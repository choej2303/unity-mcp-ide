using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers; // For PropertySuggestionCache if needed, or keep local

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Robust component resolver that avoids Assembly.LoadFrom and supports assembly definitions.
    /// Prioritizes runtime (Player) assemblies over Editor assemblies.
    /// </summary>
    public static class ComponentResolver
    {
        private static readonly Dictionary<string, Type> CacheByFqn = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Type> CacheByName = new(StringComparer.Ordinal);

        /// <summary>
        /// Resolve a Component/MonoBehaviour type by short or fully-qualified name.
        /// Prefers runtime (Player) script assemblies; falls back to Editor assemblies.
        /// Never uses Assembly.LoadFrom.
        /// </summary>
        public static bool TryResolve(string nameOrFullName, out Type type, out string error)
        {
            error = string.Empty;
            type = null!;

            // Handle null/empty input
            if (string.IsNullOrWhiteSpace(nameOrFullName))
            {
                error = "Component name cannot be null or empty";
                return false;
            }

            // 1) Exact cache hits
            if (CacheByFqn.TryGetValue(nameOrFullName, out type)) return true;
            if (!nameOrFullName.Contains(".") && CacheByName.TryGetValue(nameOrFullName, out type)) return true;
            type = Type.GetType(nameOrFullName, throwOnError: false);
            if (IsValidComponent(type)) { Cache(type); return true; }

            // 2) Search loaded assemblies (prefer Player assemblies)
            var candidates = FindCandidates(nameOrFullName);
            if (candidates.Count == 1) { type = candidates[0]; Cache(type); return true; }
            if (candidates.Count > 1) { error = Ambiguity(nameOrFullName, candidates); type = null!; return false; }

#if UNITY_EDITOR
            // 3) Last resort: Editor-only TypeCache (fast index)
            var tc = TypeCache.GetTypesDerivedFrom<Component>()
                              .Where(t => NamesMatch(t, nameOrFullName));
            candidates = PreferPlayer(tc).ToList();
            if (candidates.Count == 1) { type = candidates[0]; Cache(type); return true; }
            if (candidates.Count > 1) { error = Ambiguity(nameOrFullName, candidates); type = null!; return false; }
#endif

            error = $"Component type '{nameOrFullName}' not found in loaded runtime assemblies. " +
                    "Use a fully-qualified name (Namespace.TypeName) and ensure the script compiled.";
            type = null!;
            return false;
        }

        private static bool NamesMatch(Type t, string q) =>
            t.Name.Equals(q, StringComparison.Ordinal) ||
            (t.FullName?.Equals(q, StringComparison.Ordinal) ?? false);

        private static bool IsValidComponent(Type t) =>
            t != null && typeof(Component).IsAssignableFrom(t);

        private static void Cache(Type t)
        {
            if (t.FullName != null) CacheByFqn[t.FullName] = t;
            CacheByName[t.Name] = t;
        }

        private static List<Type> FindCandidates(string query)
        {
            bool isShort = !query.Contains('.');
            var loaded = AppDomain.CurrentDomain.GetAssemblies();

#if UNITY_EDITOR
            // Names of Player (runtime) script assemblies (asmdefs + Assembly-CSharp)
            var playerAsmNames = new HashSet<string>(
                UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player).Select(a => a.name),
                StringComparer.Ordinal);

            IEnumerable<System.Reflection.Assembly> playerAsms = loaded.Where(a => playerAsmNames.Contains(a.GetName().Name));
            IEnumerable<System.Reflection.Assembly> editorAsms = loaded.Except(playerAsms);
#else
            IEnumerable<System.Reflection.Assembly> playerAsms = loaded;
            IEnumerable<System.Reflection.Assembly> editorAsms = Array.Empty<System.Reflection.Assembly>();
#endif
            static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly a)
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { return rtle.Types.Where(t => t != null)!; }
            }

            Func<Type, bool> match = isShort
                ? (t => t.Name.Equals(query, StringComparison.Ordinal))
                : (t => t.FullName!.Equals(query, StringComparison.Ordinal));

            var fromPlayer = playerAsms.SelectMany(SafeGetTypes)
                                       .Where(IsValidComponent)
                                       .Where(match);
            var fromEditor = editorAsms.SelectMany(SafeGetTypes)
                                       .Where(IsValidComponent)
                                       .Where(match);

            var list = new List<Type>(fromPlayer);
            if (list.Count == 0) list.AddRange(fromEditor);
            return list;
        }

#if UNITY_EDITOR
        private static IEnumerable<Type> PreferPlayer(IEnumerable<Type> seq)
        {
            var player = new HashSet<string>(
                UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player).Select(a => a.name),
                StringComparer.Ordinal);

            return seq.OrderBy(t => player.Contains(t.Assembly.GetName().Name) ? 0 : 1);
        }
#endif

        private static string Ambiguity(string query, IEnumerable<Type> cands)
        {
            var lines = cands.Select(t => $"{t.FullName} (assembly {t.Assembly.GetName().Name})");
            return $"Multiple component types matched '{query}':\n - " + string.Join("\n - ", lines) +
                   "\nProvide a fully qualified type name to disambiguate.";
        }

        /// <summary>
        /// Gets all accessible property and field names from a component type.
        /// </summary>
        public static List<string> GetAllComponentProperties(Type componentType)
        {
            if (componentType == null) return new List<string>();

            var properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                         .Where(p => p.CanRead && p.CanWrite)
                                         .Select(p => p.Name);

            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                     .Where(f => !f.IsInitOnly && !f.IsLiteral)
                                     .Select(f => f.Name);

            // Also include SerializeField private fields (common in Unity)
            var serializeFields = componentType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                                              .Where(f => f.GetCustomAttribute<SerializeField>() != null)
                                              .Select(f => f.Name);

            return properties.Concat(fields).Concat(serializeFields).Distinct().OrderBy(x => x).ToList();
        }

        /// <summary>
        /// Uses AI to suggest the most likely property matches for a user's input.
        /// </summary>
        public static List<string> GetAIPropertySuggestions(string userInput, List<string> availableProperties)
        {
            if (string.IsNullOrWhiteSpace(userInput) || !availableProperties.Any())
                return new List<string>();

            // Simple caching to avoid repeated AI calls for the same input
            var cacheKey = $"{userInput.ToLowerInvariant()}:{string.Join(",", availableProperties)}";
            if (PropertySuggestionCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                // For now, we'll use a simple rule-based approach that mimics AI behavior
                // This can be replaced with actual AI calls later
                var suggestions = GetRuleBasedSuggestions(userInput, availableProperties);

                PropertySuggestionCache[cacheKey] = suggestions;
                return suggestions;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AI Property Matching] Error getting suggestions for '{userInput}': {ex.Message}");
                return new List<string>();
            }
        }

        private static readonly Dictionary<string, List<string>> PropertySuggestionCache = new();

        /// <summary>
        /// Rule-based suggestions that mimic AI behavior for property matching.
        /// This provides immediate value while we could add real AI integration later.
        /// </summary>
        private static List<string> GetRuleBasedSuggestions(string userInput, List<string> availableProperties)
        {
            var suggestions = new List<string>();
            var cleanedInput = userInput.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

            foreach (var property in availableProperties)
            {
                var cleanedProperty = property.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

                // Exact match after cleaning
                if (cleanedProperty == cleanedInput)
                {
                    suggestions.Add(property);
                    continue;
                }

                // Check if property contains all words from input
                var inputWords = userInput.ToLowerInvariant().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (inputWords.All(word => cleanedProperty.Contains(word.ToLowerInvariant())))
                {
                    suggestions.Add(property);
                    continue;
                }

                // Levenshtein distance for close matches
                if (LevenshteinDistance(cleanedInput, cleanedProperty) <= Math.Max(2, cleanedInput.Length / 4))
                {
                    suggestions.Add(property);
                }
            }

            // Prioritize exact matches, then by similarity
            return suggestions.OrderBy(s => LevenshteinDistance(cleanedInput, s.ToLowerInvariant().Replace(" ", "")))
                             .Take(3)
                             .ToList();
        }

        /// <summary>
        /// Calculates Levenshtein distance between two strings for similarity matching.
        /// </summary>
        private static int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            var matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(Math.Min(
                        matrix[i - 1, j] + 1,      // deletion
                        matrix[i, j - 1] + 1),     // insertion
                        matrix[i - 1, j - 1] + cost); // substitution
                }
            }

            return matrix[s1.Length, s2.Length];
        }
    }
}
