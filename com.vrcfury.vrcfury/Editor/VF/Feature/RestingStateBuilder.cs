using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    /**
     * This builder is in charge of changing the resting state of the avatar for all the other builders.
     * If two builders make a conflicting decision, something is wrong (perhaps the user gave conflicting instructions?)
     */
    public class RestingStateBuilder : FeatureBuilder {

        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FixWriteDefaultsBuilder writeDefaultsManager;
        private readonly List<PendingClip> pendingClips = new List<PendingClip>();

        public class PendingClip {
            public AnimationClip clip;
            public string owner;
        }

        public void ApplyClipToRestingState(AnimationClip clip, bool recordDefaultStateFirst = false) {
            if (recordDefaultStateFirst) {
                foreach (var b in clip.GetFloatBindings())
                    writeDefaultsManager.RecordDefaultNow(b, true);
                foreach (var b in clip.GetObjectBindings())
                    writeDefaultsManager.RecordDefaultNow(b, false);
            }

            var copy = new AnimationClip();
            copy.CopyFrom(clip);
            pendingClips.Add(new PendingClip { clip = copy, owner = manager.GetCurrentlyExecutingFeatureName() });
            mover.AddAdditionalManagedClip(copy);
        }

        /**
         * There are three phases that resting state can be applied from,
         * (1) ForceObjectState, (2) Toggles and other things, (3) Toggle Rest Pose
         * Conflicts are allowed between phases, but not within a phase.
         */
        [FeatureBuilderAction(FeatureOrder.ApplyRestState1)]
        public void ApplyPendingClips() {
            foreach (var pending in pendingClips) {
                pending.clip.SampleAnimation(avatarObject, 0);
                foreach (var (binding,curve) in pending.clip.GetAllCurves()) {
                    HandleMaterialSwaps(binding, curve);
                    HandleMaterialProperties(binding, curve);
                    StoreBinding(binding, curve.GetFirst(), pending.owner);
                }
            }
            pendingClips.Clear();
            stored.Clear();
        }
        [FeatureBuilderAction(FeatureOrder.ApplyRestState2)]
        public void ApplyPendingClips2() {
            ApplyPendingClips();
        }
        [FeatureBuilderAction(FeatureOrder.ApplyRestState3)]
        public void ApplyPendingClips3() {
            ApplyPendingClips();
        }
        [FeatureBuilderAction(FeatureOrder.ApplyRestState4)]
        public void ApplyPendingClips4() {
            ApplyPendingClips();
        }


        public IEnumerable<AnimationClip> GetPendingClips() {
            return pendingClips.Select(pending => pending.clip);
        }

        private readonly Dictionary<EditorCurveBinding, StoredEntry> stored =
            new Dictionary<EditorCurveBinding, StoredEntry>();

        private class StoredEntry {
            public string owner;
            public FloatOrObject value;
        }

        public void StoreBinding(EditorCurveBinding binding, FloatOrObject value, string owner) {
            binding = binding.Normalize();
            if (stored.TryGetValue(binding, out var otherStored)) {
                if (value != otherStored.value) {
                    throw new Exception(
                        "VRCFury was told to set the resting pose of a property to two different values.\n\n" +
                        $"Property: {binding.path} {binding.propertyName}\n\n" +
                        $"{otherStored.owner} set it to {otherStored.value}\n\n" +
                        $"{owner} set it to {value}");
                }
            }
            stored[binding] = new StoredEntry() {
                owner = owner,
                value = value
            };
        }

        private void HandleMaterialSwaps(EditorCurveBinding binding, FloatOrObjectCurve curve) {
            var val = curve.GetFirst();
            if (val.IsFloat()) return;
            var newMat = val.GetObject() as Material;
            if (newMat == null) return;
            if (!binding.propertyName.StartsWith("m_Materials.Array.data[")) return;

            var start = "m_Materials.Array.data[".Length;
            var end = binding.propertyName.Length - 1;
            var str = binding.propertyName.Substring(start, end - start);
            if (!int.TryParse(str, out var num)) return;
            var transform = avatarObject.Find(binding.path);
            if (!transform) return;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return;
            var renderer = transform.GetComponent(binding.type) as Renderer;
            if (!renderer) return;
            renderer.sharedMaterials = renderer.sharedMaterials
                .Select((mat,i) => (i == num) ? newMat : mat)
                .ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }

        private void HandleMaterialProperties(EditorCurveBinding binding, FloatOrObjectCurve curve) {
            var val = curve.GetFirst();
            if (!val.IsFloat()) return;
            if (!binding.propertyName.StartsWith("material.")) return;
            var propName = binding.propertyName.Substring("material.".Length);
            var transform = avatarObject.Find(binding.path);
            if (!transform) return;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return;
            var renderer = transform.GetComponent(binding.type) as Renderer;
            if (!renderer) return;
            renderer.sharedMaterials = renderer.sharedMaterials.Select(mat => {
                if (mat == null) return mat;

                var type = mat.GetPropertyType(propName);
                if (type == ShaderUtil.ShaderPropertyType.Float || type == ShaderUtil.ShaderPropertyType.Range) {
                    mat = MutableManager.MakeMutable(mat);
                    mat.SetFloat(propName, val.GetFloat());
                    return mat;
                }

                if (propName.Length < 2 || propName[propName.Length-2] != '.') return mat;

                var bundleName = propName.Substring(0, propName.Length - 2);
                var bundleSuffix = propName.Substring(propName.Length - 1);
                var bundleType = mat.GetPropertyType(bundleName);
                // This is /technically/ incorrect, since if a property is missing, the proper (matching unity)
                // behaviour is that it should be set to 0. However, unit really tries to not allow you to be missing
                // one component in your animator (by deleting them all at once), so it's probably not a big deal.
                if (bundleType == ShaderUtil.ShaderPropertyType.Color) {
                    mat = MutableManager.MakeMutable(mat);
                    var color = mat.GetColor(bundleName);
                    if (bundleSuffix == "r") color.r = val.GetFloat();
                    if (bundleSuffix == "g") color.g = val.GetFloat();
                    if (bundleSuffix == "b") color.b = val.GetFloat();
                    if (bundleSuffix == "a") color.a = val.GetFloat();
                    mat.SetColor(propName, color);
                    return mat;
                }
                if (bundleType == ShaderUtil.ShaderPropertyType.Vector) {
                    mat = MutableManager.MakeMutable(mat);
                    var vector = mat.GetVector(bundleName);
                    if (bundleSuffix == "x") vector.x = val.GetFloat();
                    if (bundleSuffix == "y") vector.y = val.GetFloat();
                    if (bundleSuffix == "z") vector.z = val.GetFloat();
                    if (bundleSuffix == "w") vector.w = val.GetFloat();
                    mat.SetVector(propName, vector);
                    return mat;
                }

                return mat;
            }).ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }
    }
}
