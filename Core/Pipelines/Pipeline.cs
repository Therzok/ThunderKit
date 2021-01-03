﻿#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PassivePicasso.ThunderKit.Pipelines
{
    public class Pipeline : ScriptableObject
    {
        [MenuItem(ScriptableHelper.ThunderKitContextRoot + nameof(Pipeline), false)]
        public static void CreatePipeline() => ScriptableHelper.SelectNewAsset<Pipeline>();

        public PipelineJob[] runSteps;

        public string OutputRoot => System.IO.Path.Combine("ThunderKit");

        public virtual void Execute()
        {
            PipelineJob[] runnableSteps = runSteps.Where(step => 
                                                     step.GetType().GetCustomAttributes()
                                                         .OfType<PipelineSupportAttribute>()
                                                         .Any(psa => psa.HandlesPipeline(this.GetType()))).ToArray();
            foreach (var step in runnableSteps) 
                    step.Execute(this);
        }


        [OnOpenAsset]
        public static bool DoubleClickDeploy(int instanceID, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceID) is Pipeline instance)) return false;

            instance.Execute();

            return true;
        }
    }
}
#endif