﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThunderKit.Common;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Editor;
using ThunderKit.Core.Manifests;
using ThunderKit.Core.Manifests.Datum;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ThunderKit.Core.Pipelines
{
    public class Pipeline : ComposableObject
    {
        [MenuItem(Constants.ThunderKitContextRoot + nameof(Pipeline), false, priority = Constants.ThunderKitMenuPriority)]
        public static void Create() => ScriptableHelper.SelectNewAsset<Pipeline>();

        public Manifest manifest;

        public Manifest[] manifests { get; private set; }
        public IEnumerable<ManifestDatum> Datums => manifests.SelectMany(manifest => manifest.Data.OfType<ManifestDatum>());

        public IEnumerable<PipelineJob> Jobs => Data.OfType<PipelineJob>();

        public string OutputRoot => System.IO.Path.Combine("ThunderKit");

        public override string ElementTemplate =>
@"using ThunderKit.Core.Pipelines;

namespace {0}
{{
    [PipelineSupport(typeof(Pipeline))]
    public class {1} : PipelineJob
    {{
        public override void Execute(Pipeline pipeline)
        {{
        }}
    }}
}}
";

        public int JobIndex { get; protected set; }
        public int ManifestIndex { get; set; }
        public Manifest Manifest => manifests[ManifestIndex];

        public virtual void Execute()
        {
            manifests = manifest.EnumerateManifests().ToArray();
            PipelineJob[] jobs = Jobs.Where(SupportsType).ToArray();
            for (JobIndex = 0; JobIndex < jobs.Length; JobIndex++)
            {
                Job().Errored = false;
                Job().ErrorMessage = string.Empty;
            }
            for (JobIndex = 0; JobIndex < jobs.Length; JobIndex++)
                try
                {
                    if (!Job().Active) continue;
                    if (JobIsManifestProcessor())
                        ExecuteManifestLoop();
                    else
                        ExecuteJob();
                }
                catch (Exception e)
                {
                    Job().Errored = true;
                    Job().ErrorMessage = e.Message;
                    EditorGUIUtility.PingObject(Job());
                    Debug.LogError($"Error Invoking {Job().name} Job on Pipeline {name}\r\n{e}", this);
                    JobIndex = jobs.Length;
                    break;
                }

            JobIndex = -1;

            PipelineJob Job() => jobs[JobIndex];

            void ExecuteJob() => Job().Execute(this);

            bool JobIsManifestProcessor() => Job().GetType().GetCustomAttributes<ManifestProcessorAttribute>().Any();

            bool CanProcessManifest(RequiresManifestDatumTypeAttribute attribute) => attribute.CanProcessManifest(Manifest);

            bool JobCanProcessManifest() => Job().GetType().GetCustomAttributes<RequiresManifestDatumTypeAttribute>().All(CanProcessManifest);

            void ExecuteManifestLoop()
            {
                var manifests = manifest.EnumerateManifests().ToArray();
                for (ManifestIndex = 0; ManifestIndex < manifests.Length; ManifestIndex++)
                    if (Manifest && JobCanProcessManifest())
                        ExecuteJob();

                ManifestIndex = -1;
            }
            //I really can't justify why I designed this like this, but I did, you already saw it, its too late.
        }


        [OnOpenAsset]
        public static bool DoubleClickDeploy(int instanceID, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceID) is Pipeline instance)) return false;

            instance.Execute();

            return true;
        }

        public bool SupportsType(PipelineJob job) => SupportsType(job.GetType());
        public override bool SupportsType(Type type)
        {
            if (ElementType.IsAssignableFrom(type))
            {
                var customAttributes = type.GetCustomAttributes();
                var pipelineSupportAttributes = customAttributes.OfType<PipelineSupportAttribute>();
                if (pipelineSupportAttributes.Any(psa => psa.HandlesPipeline(GetType())))
                    return true;
            }
            return false;
        }

        public override Type ElementType => typeof(PipelineJob);
    }
}