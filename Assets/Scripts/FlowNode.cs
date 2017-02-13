﻿using UnityEngine;
using System.Collections.Generic;
using NodeCanvas.Framework;
using NodeCanvas;
using System.Linq;
using System;
using ParadoxNotion;
using System.Reflection;
using ParadoxNotion.Design;

namespace FlowCanvas
{
    abstract public class FlowNode : Node
    {
        private Dictionary<string, Port> inputPorts = new Dictionary<string, Port>(StringComparer.Ordinal);
        private Dictionary<string, Port> outputPorts = new Dictionary<string, Port>(StringComparer.Ordinal);
        sealed public override bool allowAsPrime { get { return false; } }

        private Port[] orderedInputs;
        private Port[] orderedOutputs;
        private ValueInput firstValuePort;
        private Dictionary<string, object> _inputPortValues;

        // be critical, this is will init inputPorts and outputPorts
        sealed public override void OnValidate(Graph flowGraph)
        {
            GatherPorts();
        }

        public void SetStatus(Status status)
        {
            this.status = status;
        }

        public BinderConnection GetOutputConnectionForPortID(string ID)
        {
            return outConnections.OfType<BinderConnection>().FirstOrDefault(c => c.sourcePortID == ID);
        }

        public void Fail(string error = null)
        {
            status = Status.Failure;
            if (error != null)
            {
                Debug.LogError(string.Format("<b>Flow Execution Error:</b> '{0}' - '{1}'", this.name, error), graph.agent);
            }
        }

        public Port GetOutputPort(string ID)
        {
            Port output = null;
            outputPorts.TryGetValue(ID, out output);
            return output;
        }

        

        

        public void GatherPorts()
        {

            inputPorts.Clear();
            outputPorts.Clear();
            RegisterPorts();

#if UNITY_EDITOR
            OnPortsGatheredInEditor();
#endif

            DeserializeInputPortValues();
            ValidateConnections();
        }

        

        void OnPortsGatheredInEditor()
        {
            orderedInputs = inputPorts.Values.OrderBy(p => p.GetType() == typeof(FlowInput) ? 0 : 1).ToArray();
            orderedOutputs = outputPorts.Values.OrderBy(p => p.GetType() == typeof(FlowOutput) ? 0 : 1).ToArray();
            firstValuePort = orderedInputs.OfType<ValueInput>().FirstOrDefault();
        }

        // Validate
        void ValidateConnections()
        {

            foreach (var cOut in outConnections.ToArray())
            { //ToArray because connection might remove itself if invalid
                if (cOut is BinderConnection)
                {
                    (cOut as BinderConnection).GatherAndValidateSourcePort();
                }
            }

            foreach (var cIn in inConnections.ToArray())
            {
                if (cIn is BinderConnection)
                {
                    (cIn as BinderConnection).GatherAndValidateTargetPort();
                }
            }
        }

        public Port GetInputPort(string ID)
        {
            Port input = null;
            inputPorts.TryGetValue(ID, out input);
            return input;
        }

        

        void DeserializeInputPortValues()
        {

            if (_inputPortValues == null)
            {
                return;
            }

            foreach (var pair in _inputPortValues)
            {
                Port inputPort = null;
                if (inputPorts.TryGetValue(pair.Key, out inputPort))
                {
                    if (inputPort is ValueInput && pair.Value != null && inputPort.type.RTIsAssignableFrom(pair.Value.GetType()))
                    {
                        (inputPort as ValueInput).serializedValue = pair.Value;
                    }
                }
            }
        }

        // notice : this is a virtual function
        virtual protected void RegisterPorts()
        {
            DoReflectionBasedRegistration();
        }

        
        void DoReflectionBasedRegistration()
        {
            //FlowInputs. All void methods with one Flow parameter.
            //example
            /*
            [Name("bbb")]
            public void Test(Flow f){} ----> flowInput
            * */
            foreach (var method in this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var parameters = method.GetParameters();
                if (method.ReturnType == typeof(void) && parameters.Length == 1 && parameters[0].ParameterType == typeof(Flow))
                {
                    var nameAtt = method.RTGetAttribute<NameAttribute>(false);
                    var name = nameAtt != null ? nameAtt.name : method.Name.SplitCamelCase();
                    var pointer = method.RTCreateDelegate<FlowHandler>(this);
                    AddFlowInput(name, pointer);
                }
            }


            //ValueOutputs. All readable public properties.
            foreach (var prop in this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (prop.CanRead)
                {
                    AddPropertyOutput(prop, this);
                }
            }


            //Search for delegates fields
            foreach (var field in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {

                if (typeof(Delegate).RTIsAssignableFrom(field.FieldType))
                {
                    //[Name("xxx")]
                    var nameAtt = field.RTGetAttribute<NameAttribute>(false);
                    var name = nameAtt != null ? nameAtt.name : field.Name.SplitCamelCase();

                    var invokeMethod = field.FieldType.GetMethod("Invoke");
                    var parameters = invokeMethod.GetParameters();

                    //FlowOutputs. All FlowHandler fields.
                    if (field.FieldType == typeof(FlowHandler))
                    {
                        var flowOut = AddFlowOutput(name);
                        field.SetValue(this, (FlowHandler)flowOut.Call);
                    }

                    //ValueInputs. All ValueHandler<T> fields.
                    if (invokeMethod.ReturnType != typeof(void) && parameters.Length == 0)
                    {
                        var delType = invokeMethod.ReturnType;
                        var portType = typeof(ValueInput<>).RTMakeGenericType(new Type[] { delType });
                        var port = (ValueInput)Activator.CreateInstance(portType, new object[] { this, name, name });

                        var getterType = typeof(ValueHandler<>).RTMakeGenericType(new Type[] { delType });
                        var getter = port.GetType().GetMethod("get_value").RTCreateDelegate(getterType, port);
                        field.SetValue(this, getter);
                        inputPorts[name] = port;
                    }
                }
            }
        }

        public FlowOutput AddFlowOutput(string name, string ID = "")
        {
            if (string.IsNullOrEmpty(ID)) ID = name;
            return (FlowOutput)(outputPorts[ID] = new FlowOutput(this, name, ID));
        }

        public FlowInput AddFlowInput(string name, FlowHandler pointer, string ID = "")
        {
            if (string.IsNullOrEmpty(ID)) ID = name;
            return (FlowInput)(inputPorts[ID] = new FlowInput(this, name, ID, pointer));
        }

        // only read property will become ValueOutput,ValueOutput's getter is only get {return value;}
        public ValueOutput AddPropertyOutput(PropertyInfo prop, object instance)
        {

            if (!prop.CanRead)
            {
                Debug.LogError("Property is write only");
                return null;
            }

            var nameAtt = prop.RTGetAttribute<NameAttribute>(false);
            var name = nameAtt != null ? nameAtt.name : prop.Name.SplitCamelCase();

            var getterType = typeof(ValueHandler<>).RTMakeGenericType(new Type[] { prop.PropertyType });
            // RTGetGetMethod : 当在派生类中重写时，返回此属性的公共或非公共 get 访问器
            var getter = prop.RTGetGetMethod().RTCreateDelegate(getterType, instance);
            var portType = typeof(ValueOutput<>).RTMakeGenericType(new Type[] { prop.PropertyType });
            var port = (ValueOutput)Activator.CreateInstance(portType, new object[] { this, name, name, getter });
            return (ValueOutput)(outputPorts[name] = port);
        }

        

        public BinderConnection GetInputConnectionForPortID(string ID)
        {
            return inConnections.OfType<BinderConnection>().FirstOrDefault(c => c.targetPortID == ID);
        }

        public void AssignSelfInstancePort()
        {
            if (graphAgent == null)
            {
                return;
            }

            var instanceInput = inputPorts.Values.OfType<ValueInput>().FirstOrDefault();
            if (instanceInput != null && !instanceInput.isConnected && instanceInput.isDefaultValue)
            {
                if (instanceInput.type == typeof(GameObject))
                {
                    instanceInput.serializedValue = graphAgent.gameObject;
                }
                if (typeof(Component).RTIsAssignableFrom(instanceInput.type))
                {
                    instanceInput.serializedValue = graphAgent.GetComponent(instanceInput.type);
                }
            }
        }

        public void BindPorts()
        {
            for (var i = 0; i < outConnections.Count; i++)
            {
                (outConnections[i] as BinderConnection).Bind();
            }
        }

    }
}

