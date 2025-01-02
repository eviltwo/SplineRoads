using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

namespace SplineRoads
{
    [ExecuteInEditMode]
    [AddComponentMenu("Spline Roads/Spline Span Instantiate")]
    [SelectionBase]
    public class SplineSpanInstantiate : SplineComponent
    {
        [SerializeField]
        public SplineContainer Container = null;

        [SerializeField]
        public SplineSpan Span = new SplineSpan();

        [Serializable]
        public class InstantiableItem
        {
            public GameObject Prefab = null;
            public float Probability = 1f;
        }

        [SerializeField]
        public List<InstantiableItem> ItemsToInstantiate = new List<InstantiableItem>
        {
            new InstantiableItem{ Prefab = null, Probability = 1f}
        };

        [SerializeField]
        public Vector3 UpAxis = Vector3.up;

        [SerializeField]
        public Vector3 ForwardAxis = Vector3.forward;

        public enum Method
        {
            InstanceCount,
            SplineDistance,
        }

        [SerializeField]
        public Method InstantiateMethod = Method.SplineDistance;

        [SerializeField]
        public Vector2 SpacingRange = new Vector2(1f, 1f);

        [Serializable]
        public class Vector3Range
        {
            public Vector3 Min = Vector3.zero;
            public Vector3 Max = Vector3.zero;
        }

        [SerializeField]
        public Vector3Range PositionOffset = default;

        [SerializeField]
        public Vector3Range RotationOffset = default;

        [SerializeField]
        public Vector3Range ScaleOffset = default;

        [SerializeField, Range(0, 1), FormerlySerializedAs("FitSlope")]
        public float FitRotationToSpline = 1f;

        [SerializeField, Range(0, 1)]
        public float FitHeightToTerrain = 0f;

        [SerializeField, Range(0, 1)]
        public float FitRotationToTerrain = 0f;

        [SerializeField]
        public int CountLimit = 1000;

        [SerializeField]
        public int RandomSeed = 0;

        private bool _isDirty;

        private void Reset()
        {
            Container = GetComponentInParent<SplineContainer>();
        }

        private void OnEnable()
        {
            if (RandomSeed == 0)
            {
                RandomSeed = GetInstanceID();
            }
            SetDirty();
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            ClearInstances();
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
        {
            if (Container != null && Container.Splines.Count > 0 && Container.Splines[Span.Index] == spline)
            {
                SetDirty();
            }
        }

        private void OnValidate()
        {
            Span.Validate(Container);
            SetDirty();
        }

        public void SetDirty()
        {
            _isDirty = true;
        }

        private void Update()
        {
            if (_isDirty && isActiveAndEnabled)
            {
                _isDirty = false;
                UpdateInstances();
            }
        }

        private void UpdateInstances()
        {
            ClearInstances();

            if (Container == null
                || Container.Splines.Count == 0
                || Span.Index < 0 || Span.Index >= Container.Splines.Count
                || ItemsToInstantiate.Count == 0)
            {
                return;
            }

            var randomStateCache = Random.state;
            Random.InitState(RandomSeed);
            try
            {
                var spline = Container.Splines[Span.Index];
                using (var nativeSpline = new NativeSpline(spline, Container.transform.localToWorldMatrix, Allocator.TempJob))
                {
                    var splineLength = nativeSpline.GetLength();
                    var spanLength = splineLength * (Span.Range.y - Span.Range.x);
                    var reservedDistance = 0f;
                    var randomBox = new RandomBox();
                    foreach (var item in ItemsToInstantiate)
                    {
                        randomBox.PushContent(item.Probability);
                    }
                    for (int itr = 0; itr < CountLimit; itr++)
                    {
                        var t = Span.Range.x + reservedDistance / splineLength;
                        if (t >= Span.Range.y)
                        {
                            continue;
                        }
                        var item = ItemsToInstantiate[randomBox.Choose()];
                        if (item.Prefab != null)
                        {
                            var instance = CreateInstance(item.Prefab);
                            CalculateTRS(nativeSpline, t, out var position, out var rotation, out var scale);
                            instance.transform.position = position;
                            instance.transform.rotation = rotation;
                            instance.transform.localScale = scale;
                        }
                        var spacing = 0f;
                        switch (InstantiateMethod)
                        {
                            case Method.InstanceCount:
                                spacing = spanLength / SpacingRange.x;
                                break;
                            case Method.SplineDistance:
                                spacing = Random.Range(SpacingRange.x, SpacingRange.y);
                                break;
                        }
                        spacing = Mathf.Max(spacing, 0.001f);
                        reservedDistance += spacing;
                    }
                }
            }
            finally
            {
                Random.state = randomStateCache;
            }
        }

        protected virtual void CalculateTRS(
            NativeSpline spline,
            float t,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            spline.Evaluate(t, out var pos, out var tan, out var up);
            var splineRotation = Quaternion.LookRotation(tan, up);

            // Position
            position = pos;
            var posOffset = Vector3.Lerp(PositionOffset.Min, PositionOffset.Max, Random.value);
            position += splineRotation * posOffset;
            if (FitHeightToTerrain > 0)
            {
                var terrain = Terrain.activeTerrain;
                var terrainHeight = terrain.SampleHeight(position) + terrain.GetPosition().y;
                terrainHeight += posOffset.y;
                position.y = Mathf.Lerp(pos.y, terrainHeight, FitHeightToTerrain);
            }

            // Rotation
            var remapRotation = Quaternion.Inverse(Quaternion.LookRotation(ForwardAxis, UpAxis));
            var rotOffset = Vector3.Lerp(RotationOffset.Min, RotationOffset.Max, Random.value);
            if (FitRotationToSpline == 1)
            {
                rotation = splineRotation * Quaternion.Euler(rotOffset) * remapRotation;
            }
            else
            {
                var lockAngleY = Vector3.SignedAngle(transform.forward, tan, transform.up);
                var lockYRot = Quaternion.AngleAxis(lockAngleY, transform.up);
                var slopeRot = Quaternion.Slerp(lockYRot, splineRotation, FitRotationToSpline);
                rotation = slopeRot * Quaternion.Euler(rotOffset) * remapRotation;
            }
            if (FitRotationToTerrain > 0)
            {
                var terrain = Terrain.activeTerrain;
                var localPosition = terrain.transform.InverseTransformPoint(position);
                var normal = terrain.terrainData.GetInterpolatedNormal(localPosition.x / terrain.terrainData.size.x, localPosition.z / terrain.terrainData.size.z);
                var terrainRotation = Quaternion.Lerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), FitRotationToTerrain);
                rotation = terrainRotation * rotation;
            }

            // Scale
            var scaleOffset = Vector3.Lerp(ScaleOffset.Min, ScaleOffset.Max, Random.value);
            scale = Vector3.one + scaleOffset;
        }

        private GameObject _instanceRoot;
        public GameObject InstanceRoot => _instanceRoot;

        private static readonly List<SplineSpanInstantiate> _componentBuffer = new List<SplineSpanInstantiate>();
        private static readonly List<GameObject> _unusedObjectBuffer = new List<GameObject>();

        private void ClearInstances()
        {
            if (Application.isPlaying)
            {
                Destroy(_instanceRoot);
            }
            else
            {
                DestroyImmediate(_instanceRoot);
            }
            _instanceRoot = null;

            // Destroy unused root objects.
            _componentBuffer.Clear();
            GetComponents(_componentBuffer);
            _unusedObjectBuffer.Clear();
            var childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if ((child.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0)
                {
                    continue;
                }
                var used = false;
                foreach (var component in _componentBuffer)
                {
                    if (component.InstanceRoot == child.gameObject)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used)
                {
                    _unusedObjectBuffer.Add(child.gameObject);
                }
            }
            foreach (var destroyObject in _unusedObjectBuffer)
            {
                if (Application.isPlaying)
                {
                    Destroy(destroyObject);
                }
                else
                {
                    DestroyImmediate(destroyObject);
                }
            }
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            if (_instanceRoot == null)
            {
                _instanceRoot = new GameObject("SplineSpanInstanceRoot");
                _instanceRoot.hideFlags |= HideFlags.HideAndDontSave;
                //InstanceRoot.hideFlags |= HideFlags.NotEditable | HideFlags.DontSave;
                _instanceRoot.transform.SetParent(transform, false);
            }
            var instance = Instantiate(prefab, _instanceRoot.transform);

            instance.hideFlags |= HideFlags.DontSave;
            instance.transform.SetParent(_instanceRoot.transform, false);
            return instance;
        }

        private void OnDrawGizmosSelected()
        {
            if (Container == null
                || Container.Splines.Count == 0
                || Span.Index < 0 || Span.Index >= Container.Splines.Count)
            {
                return;
            }

            var spline = Container.Splines[Span.Index];
            Gizmos.color = Color.red;
            Gizmos.matrix = Container.transform.localToWorldMatrix;
            const float LineLength = 1.0f;
            // Start
            {
                spline.Evaluate(Span.Range.x, out var pos, out var tan, out var up);
                var rot = Quaternion.LookRotation(tan, up);
                Gizmos.DrawLine((Vector3)pos + rot * (Vector3.left * LineLength), (Vector3)pos + rot * (Vector3.right * LineLength));
                Gizmos.DrawLine((Vector3)pos, (Vector3)pos + rot * (Vector3.forward * LineLength));
            }
            // End
            {
                spline.Evaluate(Span.Range.y, out var pos, out var tan, out var up);
                var rot = Quaternion.LookRotation(tan, up);
                Gizmos.DrawLine((Vector3)pos + rot * (Vector3.left * LineLength), (Vector3)pos + rot * (Vector3.right * LineLength));
                Gizmos.DrawLine((Vector3)pos, (Vector3)pos + rot * (Vector3.back * LineLength));
            }
        }
    }

    internal class RandomBox
    {
        private List<float> _weights = new List<float>();
        private float _totalWeight = 0f;

        public void ClearContents()
        {
            _weights.Clear();
            _totalWeight = 0f;
        }

        public void PushContent(float weight)
        {
            _weights.Add(weight);
            _totalWeight += weight;
        }

        public int Choose()
        {
            var randomValue = UnityEngine.Random.Range(0f, _totalWeight);
            for (int i = 0; i < _weights.Count; i++)
            {
                randomValue -= _weights[i];
                if (randomValue <= 0f)
                {
                    return i;
                }
            }
            return _weights.Count - 1;
        }
    }
}
