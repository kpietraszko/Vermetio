#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
 
class StripShaders : IPreprocessShaders {
    public int callbackOrder { get { return 0; } }
 
    public void OnProcessShader (
        Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> shaderCompilerData) {
        // Debug.Log (shader.name);
        if (shader.name == "Standard" ||
            shader.name == "Standard (Specular setup)" ||
            shader.name == "Standard (Backfaces)"
        ) {
            for (int i = 0; i < shaderCompilerData.Count; ++i) {
                shaderCompilerData.RemoveAt (i);
                --i;
            }
        }
    }
}
#endif