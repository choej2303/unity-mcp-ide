using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for traversing and querying scene hierarchy data.
    /// </summary>
    public static class SceneTraversalService
    {
        public static List<object> GetSceneHierarchyData(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return new List<object>();
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            return rootObjects.Select(go => GetGameObjectDataRecursive(go)).ToList();
        }

        /// <summary>
        /// Recursively builds a data representation of a GameObject and its children.
        /// </summary>
        public static object GetGameObjectDataRecursive(GameObject go)
        {
            if (go == null)
                return null;

            var childrenData = new List<object>();
            foreach (Transform child in go.transform)
            {
                childrenData.Add(GetGameObjectDataRecursive(child.gameObject));
            }

            var gameObjectData = new Dictionary<string, object>
            {
                { "name", go.name },
                { "activeSelf", go.activeSelf },
                { "activeInHierarchy", go.activeInHierarchy },
                { "tag", go.tag },
                { "layer", go.layer },
                { "isStatic", go.isStatic },
                { "instanceID", go.GetInstanceID() },
                {
                    "transform",
                    new
                    {
                        position = new
                        {
                            x = go.transform.localPosition.x,
                            y = go.transform.localPosition.y,
                            z = go.transform.localPosition.z,
                        },
                        rotation = new
                        {
                            x = go.transform.localRotation.eulerAngles.x,
                            y = go.transform.localRotation.eulerAngles.y,
                            z = go.transform.localRotation.eulerAngles.z,
                        },
                        scale = new
                        {
                            x = go.transform.localScale.x,
                            y = go.transform.localScale.y,
                            z = go.transform.localScale.z,
                        },
                    }
                },
                { "children", childrenData },
            };

            return gameObjectData;
        }
    }
}
