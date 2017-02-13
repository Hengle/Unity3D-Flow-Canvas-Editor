﻿using UnityEngine;
using System.Collections;
using NodeCanvas.Framework;
using System.Collections.Generic;

namespace FlowCanvas
{

    abstract public class FlowGraph : Graph {
        public override bool useLocalBlackboard { get { return false; } }

        sealed public override bool requiresAgent { get { return false; } }

        sealed public override bool requiresPrimeNode { get { return false; } }

        private bool hasInitialized;

        private List<IUpdatable> updatableNodes;

        protected override void OnGraphStarted()
        {
            // 初始化执行
            if (!hasInitialized)
            {
                updatableNodes = new List<IUpdatable>();
            }

            for (var i = 0; i < allNodes.Count; i++)
            {
                // 如果是MacroNodeWrapper 类型的Node的情况的话
                //if (allNodes[i] is MacroNodeWrapper)
                //{
                //    var macroNode = (MacroNodeWrapper)allNodes[i];
                //    if (macroNode.macro != null)
                //    {
                //        macroNode.CheckInstance();
                //        macroNode.macro.StartGraph(agent, blackboard, false, null);
                //    }
                //}

                // 把继承IUpdatable的Node添加到updatableNodes中
                if (!hasInitialized)
                {
                    if (allNodes[i] is IUpdatable)
                    {
                        updatableNodes.Add((IUpdatable)allNodes[i]);
                    }
                }
            }

            // 处理FlowNode 类型的Node
            if (!hasInitialized)
            {
                for (var i = 0; i < allNodes.Count; i++)
                {
                    if (allNodes[i] is FlowNode)
                    {
                        var flowNode = (FlowNode)allNodes[i];
                        flowNode.AssignSelfInstancePort();
                        // 绑定好flow的关系
                        flowNode.BindPorts();
                    }
                }
            }

            // 初始化完成
            hasInitialized = true;
        }

        protected override void OnGraphUpdate()
        {
            if (updatableNodes != null && updatableNodes.Count > 0)
            {
                for (var i = 0; i < updatableNodes.Count; i++)
                {
                    updatableNodes[i].Update();
                }
            }
        }

        protected override void OnGraphStoped()
        {
            //for (var i = 0; i < allNodes.Count; i++)
            //{
            //    var node = allNodes[i];
            //    if (node is MacroNodeWrapper)
            //    {
            //        var macroNode = (MacroNodeWrapper)node;
            //        if (macroNode.macro != null)
            //        {
            //            macroNode.macro.Stop();
            //        }
            //    }
            //}
        }
    }
}
