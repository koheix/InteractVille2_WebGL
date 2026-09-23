// WebGL ビルドをコマンドラインから作るためのエディタスクリプト（ビルドには含まれない）。
//
//   $env:IV2_WEBGL_OUT = "<出力先のフォルダ>"
//   & "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe" -batchmode -nographics `
//       -projectPath . -buildTarget WebGL -executeMethod WebGLBuilder.Build -logFile <ログ>
//
// 出力先のフォルダ名が Build/ 内のファイル名になる（公開中のページは IV2_WebGL_ver1_0_0）。
// ビルド対象のシーンは EditorBuildSettings（Build Profiles のシーン一覧）に従う。
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuilder
{
    private const string OutputEnv = "IV2_WEBGL_OUT";

    public static void Build()
    {
        // 終了コードを返すために EditorApplication.Exit を呼ぶので、batchmode 以外（エディタから直接）では動かさない。
        // 呼ぶとエディタごと終了してしまう。
        if (!Application.isBatchMode)
        {
            Debug.LogError("[WebGLBuilder] コマンドライン（-batchmode）から実行してください。エディタからは Build Profiles でビルドします。");
            return;
        }

        string output = Environment.GetEnvironmentVariable(OutputEnv);
        if (string.IsNullOrWhiteSpace(output))
        {
            Debug.LogError($"[WebGLBuilder] 環境変数 {OutputEnv} に出力先を指定してください。");
            EditorApplication.Exit(2);
            return;
        }

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Debug.Log($"[WebGLBuilder] scenes={string.Join(", ", scenes)} output={output}");

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        BuildSummary summary = report.summary;
        Debug.Log($"[WebGLBuilder] result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} size={summary.totalSize} time={summary.totalTime}");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
