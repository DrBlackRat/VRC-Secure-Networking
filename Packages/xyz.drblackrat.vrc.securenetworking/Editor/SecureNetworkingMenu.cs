using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor;

namespace DrBlackRat.VRC.SecureNetworking.Editor
{
    public static class SecureNetworkingMenu
    {
        [MenuItem("Tools/DrBlackRat/Secure Networking/Open Demo Scene", false, 1)]
        public static void OpenDemoScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Packages/xyz.drblackrat.vrc.securenetworking/Runtime/Demo Scene.unity");
            }
        }
        
        [MenuItem("Tools/DrBlackRat/Secure Networking/ Add a Secure Networking Instance Prefab to Scene", false, 2)]
        public static void AddSecureNetworkingInstancePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/xyz.drblackrat.vrc.securenetworking/Runtime/Secure Networking Instance.prefab");
            PrefabUtility.InstantiatePrefab(prefab);
        }     
    }
}
