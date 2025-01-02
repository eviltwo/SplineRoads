using System;
using UnityEditor;
using UnityEngine;

namespace SplineRoads.Editor
{
    [CustomEditor(typeof(SplineSpanInstantiate))]
    public class SplineSpanInstantiateEditor : UnityEditor.Editor
    {
        private bool _isRandomPositionOffset;
        private static bool IsFoldoutPositionOffset;
        private bool _isRandomRotationOffset;
        private static bool IsFoldoutRotationOffset;
        private bool _isRandomScaleOffset;
        private static bool IsFoldoutScaleOffset;
        private static bool IsFoldoutTerrainSettings;
        private static (string, Vector3)[] DirectionNames = new[]
        {
            ("X", Vector3.right),
            ("Y", Vector3.up),
            ("Z", Vector3.forward),
            ("-X", -Vector3.right),
            ("-Y", -Vector3.up),
            ("-Z", -Vector3.forward),
        };

        protected virtual void OnEnable()
        {
            _isRandomPositionOffset = IsRandomRange(serializedObject.FindProperty(nameof(SplineSpanInstantiate.PositionOffset)));
            _isRandomRotationOffset = IsRandomRange(serializedObject.FindProperty(nameof(SplineSpanInstantiate.RotationOffset)));
            _isRandomScaleOffset = IsRandomRange(serializedObject.FindProperty(nameof(SplineSpanInstantiate.ScaleOffset)));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.Container)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.Span)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.ItemsToInstantiate)));
            DrawDirection("Up", serializedObject.FindProperty(nameof(SplineSpanInstantiate.UpAxis)));
            DrawDirection("Forward", serializedObject.FindProperty(nameof(SplineSpanInstantiate.ForwardAxis)));
            DrawVector3Range(serializedObject.FindProperty(nameof(SplineSpanInstantiate.PositionOffset)), ref _isRandomPositionOffset, ref IsFoldoutPositionOffset);
            DrawVector3Range(serializedObject.FindProperty(nameof(SplineSpanInstantiate.RotationOffset)), ref _isRandomRotationOffset, ref IsFoldoutRotationOffset);
            DrawVector3Range(serializedObject.FindProperty(nameof(SplineSpanInstantiate.ScaleOffset)), ref _isRandomScaleOffset, ref IsFoldoutScaleOffset);
            using (var foldoutScope = new FoldoutHeaderGroupScope(IsFoldoutTerrainSettings, "Terrain"))
            using (new EditorGUI.IndentLevelScope())
            {
                IsFoldoutTerrainSettings = foldoutScope.foldout;
                if (IsFoldoutTerrainSettings)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.FitHeightToTerrain)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.FitRotationToTerrain)));
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.FitRotationToSpline)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.CountLimit)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SplineSpanInstantiate.RandomSeed)));

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsRandomRange(SerializedProperty vector3RangeProperty)
        {
            var min = vector3RangeProperty.FindPropertyRelative(nameof(SplineSpanInstantiate.Vector3Range.Min)).vector3Value;
            var max = vector3RangeProperty.FindPropertyRelative(nameof(SplineSpanInstantiate.Vector3Range.Max)).vector3Value;
            return min != max;
        }

        private static void DrawDirection(string label, SerializedProperty directionProperty)
        {
            var direction = directionProperty.vector3Value;
            var directionIndex = Array.FindIndex(DirectionNames, x => x.Item2 == direction);
            var directionIndexNew = EditorGUILayout.Popup(label, directionIndex, Array.ConvertAll(DirectionNames, x => x.Item1));
            if (directionIndex != directionIndexNew)
            {
                directionProperty.vector3Value = DirectionNames[directionIndexNew].Item2;
            }
        }

        private static GUIContent BlankContent = new GUIContent(" ");
        private static void DrawVector3Range(SerializedProperty vector3RangeProperty, ref bool isRandom, ref bool foldout)
        {
            var minProperty = vector3RangeProperty.FindPropertyRelative(nameof(SplineSpanInstantiate.Vector3Range.Min));
            var maxProperty = vector3RangeProperty.FindPropertyRelative(nameof(SplineSpanInstantiate.Vector3Range.Max));

            using (new EditorGUILayout.HorizontalScope())
            using (var foldoutScope = new FoldoutHeaderGroupScope(foldout, vector3RangeProperty.displayName))
            {
                foldout = foldoutScope.foldout;
                using (var changeCheck = new EditorGUI.ChangeCheckScope())
                {
                    isRandom = GUILayout.Toggle(isRandom, "Ranmdom", GUI.skin.button, GUILayout.ExpandWidth(false));
                    if (changeCheck.changed && !isRandom)
                    {
                        maxProperty.vector3Value = minProperty.vector3Value;
                    }
                }
            }
            using (new EditorGUI.IndentLevelScope())
            {
                if (foldout)
                {
                    using (var changeCheck = new EditorGUI.ChangeCheckScope())
                    {
                        if (isRandom)
                        {
                            EditorGUILayout.PropertyField(minProperty);
                            EditorGUILayout.PropertyField(maxProperty);
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(minProperty, BlankContent);
                        }

                        if (changeCheck.changed && !isRandom)
                        {
                            maxProperty.vector3Value = minProperty.vector3Value;
                        }
                    }
                }
            }
        }
    }

    public class FoldoutHeaderGroupScope : IDisposable
    {
        public bool foldout;

        public FoldoutHeaderGroupScope(bool foldout, string label)
        {
            this.foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, label);
        }

        public void Dispose()
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
