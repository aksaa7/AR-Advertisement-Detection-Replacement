// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.SceneManagement;

namespace PassthroughCameraSamples
{
    internal static class RequestPermissionsOnce
    {
        private static bool s_permissionsRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            // AfterSceneLoad runs after the initial sceneLoaded event. Handle the
            // active scene explicitly so builds that launch a sample scene directly
            // still request the required permissions.
            RequestPermissionsForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            RequestPermissionsForScene(scene);
        }

        private static void RequestPermissionsForScene(Scene scene)
        {
            if (scene.name == "StartScene" || s_permissionsRequested)
            {
                return;
            }

            s_permissionsRequested = true;
            OVRPermissionsRequester.Request(new[]
            {
                OVRPermissionsRequester.Permission.Scene,
                OVRPermissionsRequester.Permission.PassthroughCameraAccess
            });
        }
    }
}
