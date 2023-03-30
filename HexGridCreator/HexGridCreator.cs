using System;
using System.Collections.Generic;
using System.Linq;
using HexGridCreator.Utils;
using UnityEditor;
using UnityEngine;

namespace HexGridCreator
{
    public class HexGridCreator : EditorWindow
    {
        private static float _radius = 5.0f;
        private static float _convertedRadius = 5.0f;
        private static int _selectedRadiusType = 0;
        private static readonly string[] RadiusTypes = { "Outer", "Inner" };
        private static int _selectedOrientation = 0;
        private static readonly string[] Orientations = { "Flat-Top", "Pointy-Top" };
        private static bool _pressed = false;
        private static GameObject? _selectedGameObject;
        
        private static HexRadius _radiusType;
        private static HexOrientation _orientation;

        [MenuItem("Tools/Hex Grid Creator")]
        private static void Init()
        {
            var window = (HexGridCreator)GetWindow(typeof(HexGridCreator));
            window.titleContent.text = "Hex Grid Creator";
            window.maxSize = new Vector2(300, 65);
            window.minSize = window.maxSize;
            SceneView.duringSceneGui += OnSceneGUIDelegate;
            window.Show();
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUIDelegate;
        }

        private static void OnSceneGUIDelegate(SceneView sceneView)
        {
            HandleUtility.Repaint();
            if (!_pressed) return;
            DrawGizmos(_orientation);
            AlignSelectedHex(_orientation);
        }

        private void OnGUI()
        {
            Draw();
        }

        private static void Draw()
        {
            GUILayout.BeginVertical();

            GUI.enabled = !_pressed;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Radius", GUILayout.Width(100));
            _radius = EditorGUILayout.FloatField(_radius, GUILayout.Width(50));
            _selectedRadiusType = EditorGUILayout.Popup(_selectedRadiusType, RadiusTypes); 
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Orientation", GUILayout.Width(100));
            _selectedOrientation = EditorGUILayout.Popup(_selectedOrientation, Orientations); 
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            
            _radiusType = _selectedRadiusType == 0 ? HexRadius.Outer : HexRadius.Inner;
            _orientation = _selectedOrientation == 0 ? HexOrientation.FlatTop : HexOrientation.PointyTop;
            _convertedRadius = ConvertInnerRadiusToOuterByType(_radiusType, _radius);
            
            EditorGUI.BeginChangeCheck();
            _pressed = GUILayout.Toggle(_pressed, "Show / Hide", "Button");
            if (EditorGUI.EndChangeCheck())
            {
                _selectedGameObject = _pressed ? Selection.activeGameObject : null;
            }

            GUILayout.EndVertical();
        }

        private static void AlignSelectedHex(HexOrientation orientation)
        {
            var selectedHex = Selection.activeGameObject;
            if (selectedHex == null || selectedHex == _selectedGameObject) return;

            var selectedHexLocalPosition = selectedHex.transform.localPosition;
            var position = orientation switch
            {
                HexOrientation.FlatTop => GetPositionFlatTop(GetAlignedPositionFlatTop(selectedHexLocalPosition, _convertedRadius), _convertedRadius),
                HexOrientation.PointyTop => GetPositionPointyTop(GetAlignedPositionPointyTop(selectedHexLocalPosition, _convertedRadius), _convertedRadius),
                _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null)
            };
            
            selectedHex.transform.localPosition = position;
        }

        private static void DrawGizmos(HexOrientation orientation)
        {
            if (_selectedGameObject == null) return;
            
            var childrenTransforms = _selectedGameObject.transform.GetAllChildrenTransform();
            if (childrenTransforms.Count > 0)
            {
                var leftTransformsPositions = orientation switch
                {
                    HexOrientation.FlatTop => childrenTransforms.Select(transform =>
                        GetAlignedPositionFlatTop(transform.localPosition, _convertedRadius)).ToList(),
                    HexOrientation.PointyTop => childrenTransforms.Select(transform =>
                        GetAlignedPositionPointyTop(transform.localPosition, _convertedRadius)).ToList(),
                    _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null)
                };
                
                var emptyHexes = GetEmptyHexes(new List<Vector2Int>() { Vector2Int.zero }, leftTransformsPositions,
                    new HashSet<Vector2Int>());

                foreach (var emptyHex in emptyHexes)
                {
                    var position = orientation switch
                    {
                        HexOrientation.FlatTop => GetPositionFlatTop(emptyHex, _convertedRadius),
                        HexOrientation.PointyTop => GetPositionPointyTop(emptyHex, _convertedRadius),
                        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null)
                    };
                    
                    var vertices = GetVertices(position, _orientation, _radiusType);
                    DrawHexGizmos(vertices);
                }
            }
            else
            {
                var vertices = GetVertices(_selectedGameObject.transform.localPosition, _orientation, _radiusType);
                DrawHexGizmos(vertices);
            }
        }

        private static List<Vector2Int> GetEmptyHexes(List<Vector2Int> emptyHexes, ICollection<Vector2Int> leftTransforms, ISet<Vector2Int> visitedHexes)
        {
            if (leftTransforms.Count == 0)
            {
                return emptyHexes;
            }
            
            try
            {
                var hex = emptyHexes.First(x => leftTransforms.Any(y => y == x));
                visitedHexes.Add(hex);
                leftTransforms.Remove(hex);
                
                var neighbors = GetNeighbors(hex);
                var newEmptyHexes = new HashSet<Vector2Int>(emptyHexes);
                newEmptyHexes.UnionWith(neighbors);
                newEmptyHexes.ExceptWith(visitedHexes);

                return GetEmptyHexes(newEmptyHexes.ToList(), leftTransforms, visitedHexes);
            }
            catch (Exception)
            {
                return emptyHexes;
            }
        }

        private static void DrawHexGizmos(IReadOnlyList<Vector3> vertices)
        {
            Handles.color = Color.white;
            Handles.DrawLine(vertices[0], vertices[1]);
            Handles.DrawLine(vertices[0], vertices[2]);
            Handles.DrawLine(vertices[0], vertices[3]);
            Handles.DrawLine(vertices[0], vertices[4]);
            Handles.DrawLine(vertices[0], vertices[5]);
            Handles.DrawLine(vertices[0], vertices[6]);
            
            Handles.DrawLine(vertices[1], vertices[2]);
            Handles.DrawLine(vertices[2], vertices[3]);
            Handles.DrawLine(vertices[3], vertices[4]);
            Handles.DrawLine(vertices[4], vertices[5]);
            Handles.DrawLine(vertices[5], vertices[6]);
            Handles.DrawLine(vertices[6], vertices[1]);
        }

        private static Vector3[] GetVertices(Vector3 center, HexOrientation hexOrientation, HexRadius hexRadius)
        {
            var radius = hexRadius == HexRadius.Outer ? _convertedRadius : _convertedRadius * 2 / Mathf.Sqrt(3);
            var additionalRotation = hexOrientation == HexOrientation.PointyTop ? 0.0f : -30.0f;

            radius /= 2;
            
            var points = new Vector3[7];
            points[0] = center;

            for (var i = 0; i < 6; i++)
            {
                var angleDeg = 60 * i + additionalRotation;
                var angleRad = Mathf.PI / 180 * angleDeg;

                var x = radius * Mathf.Sin(angleRad);
                var z = radius * Mathf.Cos(angleRad);

                points[i + 1] = new Vector3(x, 0.0f, z) + center;
            }

            return points;
        }

        private static Vector2Int GetAlignedPositionFlatTop(Vector3 position, float radius)
        {
            var q = 2.0f / 3 * position.x / radius;
            var r = (-1.0f / 3 * position.x + Mathf.Sqrt(3) / 3 * position.z) / radius;
            var s = -q - r;
            
            var roundedX = Mathf.RoundToInt(q);
            var roundedY = Mathf.RoundToInt(r);
            var roundedZ = Mathf.RoundToInt(s);
            
            var xDiff = Mathf.Abs(roundedX - q);
            var yDiff = Mathf.Abs(roundedY - r);
            var zDiff = Mathf.Abs(roundedZ - s);
            
            if (xDiff > yDiff && xDiff > zDiff)
            {
                roundedX = -roundedY - roundedZ;
            }
            else if (yDiff > zDiff)
            {
                roundedY = -roundedX - roundedZ;
            }

            return new Vector2Int(roundedX, roundedY);
        }
        
        private static Vector2Int GetAlignedPositionPointyTop(Vector3 position, float radius)
        {
            var q = (Mathf.Sqrt(3)/3 * position.x - 1.0f/3 * position.z) / radius;
            var r = 2.0f/3 * position.z / radius;
            var s = -q - r;
            
            var roundedX = Mathf.RoundToInt(q);
            var roundedY = Mathf.RoundToInt(r);
            var roundedZ = Mathf.RoundToInt(s);
            
            var xDiff = Mathf.Abs(roundedX - q);
            var yDiff = Mathf.Abs(roundedY - r);
            var zDiff = Mathf.Abs(roundedZ - s);
            
            if (xDiff > yDiff && xDiff > zDiff)
            {
                roundedX = -roundedY - roundedZ;
            }
            else if (yDiff > zDiff)
            {
                roundedY = -roundedX - roundedZ;
            }

            return new Vector2Int(roundedX, roundedY);
        }
        
        private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int position)
        {
            return new List<Vector2Int>()
            {
                position + new Vector2Int(1, 0),
                position + new Vector2Int(1, -1),
                position + new Vector2Int(0, -1),
                position + new Vector2Int(-1, 0),
                position + new Vector2Int(-1, 1),
                position + new Vector2Int(0, 1)
            };
        }

        private static Vector3 GetPositionFlatTop(Vector2Int position, float radius)
        {
            var x = radius * (3.0f / 2 * position.x);
            var y = radius * (Mathf.Sqrt(3) / 2 * position.x + Mathf.Sqrt(3) * position.y);
            return new Vector3(x, 0.0f, y);
        }
        
        private static Vector3 GetPositionPointyTop(Vector2Int position, float radius)
        {
            var x = radius * (Mathf.Sqrt(3) * position.x + Mathf.Sqrt(3) / 2 * position.y);
            var y = radius * (3.0f / 2 * position.y);
            return new Vector3(x, 0.0f, y);
        }
        
        private static float ConvertInnerRadiusToOuterByType(HexRadius radiusType, float radius)
        {
            return radiusType switch
            {
                HexRadius.Outer => radius,
                HexRadius.Inner => radius * 2 / Mathf.Sqrt(3),
                _ => throw new ArgumentOutOfRangeException(nameof(radiusType), radiusType, null)
            };
        }
    }
}
