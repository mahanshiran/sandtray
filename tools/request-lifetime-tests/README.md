# Editor request lifetime regression

Reproduce in an isolated Unity 2022.3 project with `UnityRequestLifetime.cs`,
`RequestLifetimeProbe.cs`, and Editor scripts `UnityRequestReloadCleanup.cs`
and `RequestLifetimeCheck.cs`. Enable `com.unity.modules.unitywebrequest`.
Run `python3 slow_server.py` and then Unity with
`-batchmode -executeMethod RequestLifetimeCheck.Run -projectPath <test-project>`.

The check enters/exits Play Mode three times with outstanding GET and raw-upload
POST requests. `result.txt` must report PASS and the Unity log must contain no
`invalid GC handle`, unexpected completion, or disposal exceptions. The local
server uses no account credentials and never contacts production.
