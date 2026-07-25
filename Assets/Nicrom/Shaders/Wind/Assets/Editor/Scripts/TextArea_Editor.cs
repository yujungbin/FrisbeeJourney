using System;
using UnityEditor;
using UnityEngine;

namespace Nicrom.LPW
{
    [CustomEditor(typeof(TextArea))]
    public class TextArea_Editor : Editor
    {
        private SerializedProperty textContent;

        private TextArea textArea;
        private Texture2D discordIcon;
        private Texture2D publiserIcon;
        private Texture2D reviewIcon;


        private void OnEnable()
        {
            textContent = serializedObject.FindProperty("textContent");
            publiserIcon = Resources.Load<Texture2D>("LPW_Footer_Publisher");
            discordIcon = Resources.Load<Texture2D>("LPW_Footer_Discord");
            reviewIcon = Resources.Load<Texture2D>("LPW_Footer_Review");
        }
        public override void OnInspectorGUI()
        {
            textArea = (TextArea) target;

            serializedObject.Update();

            EditorGUILayout.PropertyField(textContent, new GUIContent(""), GUILayout.Height(120));
            EditorGUILayout.Space();
            DrawFooterButtons();

            serializedObject.ApplyModifiedProperties();
        }

        public void DrawFooterButtons()
        {
            var fullRect = GUILayoutUtility.GetRect(0, 0, 1, 0);
            var topLineRect = new Rect(0, fullRect.y, fullRect.xMax + 10, 1);

            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent(discordIcon, "Join Discord server"), new GUILayoutOption[2]
            {
                GUILayout.MaxHeight(40f),
                GUILayout.MaxWidth(40f),
            }))
                Application.OpenURL("https://discord.com/invite/RCdETwg");

            if (GUILayout.Button(new GUIContent(publiserIcon, "Open Asset Store page"), new GUILayoutOption[2]
{
                GUILayout.MaxHeight(40f),
                GUILayout.MaxWidth(40f),
}))
                Application.OpenURL("https://assetstore.unity.com/publishers/12903");

            if (GUILayout.Button(new GUIContent(reviewIcon, "Write review for LPW"), new GUILayoutOption[2]
            {
                GUILayout.MaxHeight(40f),
                GUILayout.MaxWidth(40f),
            }))
                Application.OpenURL("https://assetstore.unity.com/packages/vfx/shaders/low-poly-wind-182586#reviews");

            EditorGUILayout.Space();
            EditorGUILayout.EndHorizontal();
        }

    }
}
