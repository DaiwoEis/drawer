
System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.TypeInitializationException: The type initializer for 'UnityEditor.U2D.Common.BurstedBlit_00000005$BurstDirectCall' threw an exception. ---> System.InvalidOperationException: Burst failed to compile the function pointer `Void BurstedBlit$BurstManaged(UnityEngine.Color32*, Unity.Mathematics.int4 ByRef, Unity.Mathematics.int4 ByRef, Int32, Int32, UnityEngine.Color32*)`
  at Unity.Burst.BurstCompiler.Compile (System.Object delegateObj, System.Reflection.MethodInfo methodInfo, System.Boolean isFunctionPointer, System.Boolean isILPostProcessing) [0x0015a] in /Users/klutz/Downloads/draw/draw/Library/PackageCache/com.unity.burst@1.8.18/Runtime/BurstCompiler.cs:462
  at Unity.Burst.BurstCompiler.CompileILPPMethod2 (System.RuntimeMethodHandle burstMethodHandle) [0x0003a] in /Users/klutz/Downloads/draw/draw/Library/PackageCache/com.unity.burst@1.8.18/Runtime/BurstCompiler.cs:223
  at UnityEditor.U2D.Common.ImagePacker+BurstedBlit_00000005$BurstDirectCall.Constructor () [0x00000] in <785f27fbf498402380ebed380454d09c>:0
  at UnityEditor.U2D.Common.ImagePacker+BurstedBlit_00000005$BurstDirectCall..cctor () [0x00000] in <785f27fbf498402380ebed380454d09c>:0
   --- End of inner exception stack trace ---
  at $BurstDirectCallInitializer.Initialize () [0x00000] in <785f27fbf498402380ebed380454d09c>:0
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x0006a] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
   --- End of inner exception stack trace ---
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x00083] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
  at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) [0x00000] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
  at UnityEditor.EditorAssemblies.ProcessInitializeOnLoadMethodAttributes () [0x000a5] in /Users/bokken/buildslave/unity/build/Editor/Mono/EditorAssemblies.cs:147
UnityEditor.EditorAssemblies:ProcessInitializeOnLoadMethodAttributes () (at /Users/bokken/buildslave/unity/build/Editor/Mono/EditorAssemblies.cs:151)

System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.TypeInitializationException: The type initializer for 'UnityEngine.U2D.Animation.ValidateBoneWeights_000000F1$BurstDirectCall' threw an exception. ---> System.InvalidOperationException: Burst failed to compile the function pointer `Boolean ValidateBoneWeights$BurstManaged(Unity.Collections.NativeSlice `1[UnityEngine.BoneWeight] ByRef, Int32)`
  at Unity.Burst.BurstCompiler.Compile (System.Object delegateObj, System.Reflection.MethodInfo methodInfo, System.Boolean isFunctionPointer, System.Boolean isILPostProcessing) [0x0015a] in /Users/klutz/Downloads/draw/draw/Library/PackageCache/com.unity.burst@1.8.18/Runtime/BurstCompiler.cs:462
  at Unity.Burst.BurstCompiler.CompileILPPMethod2 (System.RuntimeMethodHandle burstMethodHandle) [0x0003a] in /Users/klutz/Downloads/draw/draw/Library/PackageCache/com.unity.burst@1.8.18/Runtime/BurstCompiler.cs:223
  at UnityEngine.U2D.Animation.BurstedSpriteSkinUtilities+ValidateBoneWeights_000000F1$BurstDirectCall.Constructor () [0x00000] in <11b9565cace444258ede12a125177816>:0
  at UnityEngine.U2D.Animation.BurstedSpriteSkinUtilities+ValidateBoneWeights_000000F1$BurstDirectCall..cctor () [0x00000] in <11b9565cace444258ede12a125177816>:0
   --- End of inner exception stack trace ---
  at $BurstDirectCallInitializer.Initialize () [0x00000] in <11b9565cace444258ede12a125177816>:0
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x0006a] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
   --- End of inner exception stack trace ---
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x00083] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
  at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) [0x00000] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
  at UnityEditor.EditorAssemblies.ProcessInitializeOnLoadMethodAttributes () [0x000a5] in /Users/bokken/buildslave/unity/build/Editor/Mono/EditorAssemblies.cs:147
UnityEditor.EditorAssemblies:ProcessInitializeOnLoadMethodAttributes () (at /Users/bokken/buildslave/unity/build/Editor/Mono/EditorAssemblies.cs:151)

System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.TypeInitializationException: The type initializer for 'UnityEditor.U2D.Animation.ValidateCollinear_0000072E$BurstDirectCall' threw an exception. ---> System.InvalidOperationException: Burst failed to compile the function pointer `Int32 ValidateCollinear$BurstManaged(Unity.Mathematics.float2*, Int32, Single)`
  at Unity.Burst.BurstCompiler.Compile (System.Object delegateObj, System.Reflection.MethodInfo methodInfo, System.Boolean isFunctionPointer, System.Boolean isILPostProcessing) [0x0015a] in /Users/klutz/Downloads/draw/draw/Library/PackageCache/com.unity.burst@1.8.18/Runtime/BurstCompiler.cs:462
  at Unity.Burst.BurstCompiler.CompileILPPMethod2 (System.RuntimeMethodHandle burstMethodHandle) [0x0003a] in /Users/klutz/Downloads/draw/draw/Library/PackageCache/com.unity.burst@1.8.18/Runtime/BurstCompiler.cs:223
  at UnityEditor.U2D.Animation.TriangulationUtility+ValidateCollinear_0000072E$BurstDirectCall.Constructor () [0x00000] in <50ee32691f8843e58d3b0fe57185cec7>:0
  at UnityEditor.U2D.Animation.TriangulationUtility+ValidateCollinear_0000072E$BurstDirectCall..cctor () [0x00000] in <50ee32691f8843e58d3b0fe57185cec7>:0
   --- End of inner exception stack trace ---
  at $BurstDirectCallInitializer.Initialize () [0x00000] in <50ee32691f8843e58d3b0fe57185cec7>:0
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x0006a] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
   --- End of inner exception stack trace ---
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x00083] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
  at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) [0x00000] in <5e2d116f98d140d0a76ec8a673a2a4ac>:0
  at UnityEditor.EditorAssemblies.ProcessInitializeOnLoadMethodAttributes () [0x000a5] in /Users/bokken/buildslave/unity/build/Editor/Mono/EditorAssemblies.cs:147
UnityEditor.EditorAssemblies:ProcessInitializeOnLoadMethodAttributes () (at /Users/bokken/buildslave/unity/build/Editor/Mono/EditorAssemblies.cs:151)
