﻿using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature {
    public class SecurityRestrictedBuilder : FeatureBuilder<SecurityRestricted> {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        
        [FeatureBuilderAction(FeatureOrder.SecurityRestricted)]
        public void Apply() {
            var security = allBuildersInRun.OfType<SecurityLockBuilder>().FirstOrDefault();
            if (security == null) {
                Debug.LogWarning("Security pin not set, restriction disabled");
                return;
            }

            var wrapper = GameObjects.Create(
                $"Security Restriction for {featureBaseObject.name}",
                featureBaseObject.parent,
                featureBaseObject.parent);
            
            mover.Move(featureBaseObject, wrapper);

            wrapper.active = false;

            var clip = new AnimationClip();
            clipBuilder.Enable(clip, wrapper);
            directTree.Add(security.GetEnabled().AsFloat(), clip);
        }

        public override string GetEditorTitle() {
            return "Security Restricted";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This object will be forcefully disabled until a Security Pin is entered in your avatar's menu." +
                "Note: You MUST have a Security Pin Number component on your avatar root with a pin number set, or this will not do anything!"
            );
        }
    }
}