#if UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class IOSBuildPostProcessor
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        // 1. Modify PBXProject (Bitcode, etc.)
        string projPath = PBXProject.GetPBXProjectPath(path);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string mainTargetGuid = proj.GetUnityMainTargetGuid();
        string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();

        // Disable Bitcode
        proj.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
        proj.SetBuildProperty(frameworkTargetGuid, "ENABLE_BITCODE", "NO");

        proj.WriteToFile(projPath);
        Debug.Log("iOS PostProcess: Disabled Bitcode.");

        // 2. Fix iPhone_Sensors.mm (Anonymous struct with C++ methods)
        string sensorsPath = Path.Combine(path, "Classes/iPhone_Sensors.mm");
        if (File.Exists(sensorsPath))
        {
            string content = File.ReadAllText(sensorsPath);
            // Target specific block with buttonCode to ensure we patch the right struct
            string targetStruct = "typedef struct\n{\n    int buttonCode;";
            string patchedStruct = "typedef struct JoystickButtonState\n{\n    int buttonCode;";

            if (content.Contains(targetStruct))
            {
                content = content.Replace(targetStruct, patchedStruct);
                File.WriteAllText(sensorsPath, content);
                Debug.Log("iOS PostProcess: Fixed iPhone_Sensors.mm typedef.");
            }
        }

        // 3. Fix Baselib_Atomic_Gcc.h (C++17 atomic memory order strictness)
        string baselibPath = Path.Combine(path, "Libraries/external/baselib/Include/C/Internal/Compiler/Baselib_Atomic_Gcc.h");
        if (File.Exists(baselibPath))
        {
            string content = File.ReadAllText(baselibPath);
            // Replace the failure order argument in __atomic_compare_exchange
            // Pattern: detail_ldst_intrinsic_##order2);
            // Replacement ensures failure order is not stronger than success order
            
            string targetCode = "detail_ldst_intrinsic_##order2);";
            string patchedCode = "(detail_ldst_intrinsic_##order2 > detail_ldst_intrinsic_##order1 ? detail_ldst_intrinsic_##order1 : detail_ldst_intrinsic_##order2));";

            if (content.Contains(targetCode))
            {
                content = content.Replace(targetCode, patchedCode);
                File.WriteAllText(baselibPath, content);
                Debug.Log("iOS PostProcess: Fixed Baselib_Atomic_Gcc.h memory order.");
            }
        }
    }
}
#endif
