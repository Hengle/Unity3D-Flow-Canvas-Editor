﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ParadoxNotion;
using ParadoxNotion.Design;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FlowCanvas.Framework
{

    abstract public partial class Node
    {
        public Rect rect;

#if UNITY_EDITOR
        
        private const float KNOB_SIZE = 20;

        private string _customName;
        private string _nodeName;

        private Port[] orderedInputs;
        private Port[] orderedOutputs;

        private string customName
        {
            get { return _customName; }
            set { _customName = value; }
        }

        virtual public string name
        {
            get
            {
                if (!string.IsNullOrEmpty(customName))
                {
                    return customName;
                }

                if (string.IsNullOrEmpty(_nodeName))
                {
                    var nameAtt = this.GetType().RTGetAttribute<NameAttribute>(false);
                    _nodeName = nameAtt != null ? nameAtt.name : GetType().FriendlyName().SplitCamelCase();
                }
                return _nodeName;
            }
            set { customName = value; }
        }


        

        public Node(Rect r)
        {
            rect = r;
        }

        public Rect InputKnobRect
        {
            get
            {
                return new Rect(
                    rect.x - KNOB_SIZE,
                    rect.y + (rect.height - KNOB_SIZE) / 2,
                    KNOB_SIZE, KNOB_SIZE
                    );
            }
        }

        public Rect OutputKnobRect
        {
            get
            {
                return new Rect(
                    rect.x + rect.width,
                    rect.y + (rect.height - KNOB_SIZE) / 2,
                    KNOB_SIZE, KNOB_SIZE
                    );
            }
        }

        public Rect TotalRect
        {
            get
            {
                return new Rect(
                    rect.x - KNOB_SIZE,
                    rect.y,
                    rect.width + KNOB_SIZE * 2,
                    rect.height
                    );
            }
        }

        
        private static GUIStyle _centerLabel = null;

        private static GUIStyle centerLabel
        {
            get
            {
                if (_centerLabel == null)
                {
                    _centerLabel = new GUIStyle("label");
                    _centerLabel.alignment = TextAnchor.UpperCenter;
                    _centerLabel.richText = true;
                }
                return _centerLabel;
            }
        }


        public void Draw()
        {
            rect = GUILayout.Window(ID, rect, DrawNodeContext, string.Empty, (GUIStyle)"window");
            DrawKnob();
        }

        void DrawNodeContext(int id)
        {
            ShowHeader();
            ShowPort();
            GUI.DragWindow();
        }

        void ShowHeader()
        {
            var finalTitle = name;
            GUILayout.Label(string.Format("<b><size=12><color=#{0}>{1}</color></size></b>", Color.white, finalTitle), centerLabel);
        }


        virtual protected void OnNodeGUI() { }

        void ShowPort()
        {
            GUI.skin.label.richText = true;
            OnNodeGUI();
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
        }




        public void DrawKnob()
        {
            //GUI.DrawTexture(InputKnobRect, _inoutKnobTex);
            //GUI.DrawTexture(OutputKnobRect, _outputKnobTex);
        }

        #endif

        
    }
}
