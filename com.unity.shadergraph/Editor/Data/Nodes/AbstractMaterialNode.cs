using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Drawing.Colors;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    abstract class AbstractMaterialNode : JsonObject, IGroupItem
    {
        [SerializeField]
        JsonRef<GroupData> m_Group = null;

        [SerializeField]
        private string m_Name;

        [SerializeField]
        private DrawState m_DrawState;

        [NonSerialized]
        bool m_HasError;

        [NonSerialized]
        bool m_IsValid = true;

        [NonSerialized]
        bool m_IsActive = true;

        [SerializeField]
        List<JsonData<MaterialSlot>> m_Slots = new List<JsonData<MaterialSlot>>();

        public GraphData owner { get; set; }

        OnNodeModified m_OnModified;

        public GroupData group
        {
            get => m_Group;
            set
            {
                if (m_Group == value)
                    return;

                m_Group = value;
                Dirty(ModificationScope.Topological);
            }
        }

        public void RegisterCallback(OnNodeModified callback)
        {
            m_OnModified += callback;
        }

        public void UnregisterCallback(OnNodeModified callback)
        {
            m_OnModified -= callback;
        }

        public void Dirty(ModificationScope scope)
        {
            if (m_OnModified != null)
                m_OnModified(this, scope);
        }

        public string name
        {
            get { return m_Name; }
            set { m_Name = value; }
        }

        public string[] synonyms;

        protected virtual string documentationPage => name;
        public virtual string documentationURL => NodeUtils.GetDocumentationString(documentationPage);

        public virtual bool canDeleteNode => owner != null && owner.outputNode != this;

        public DrawState drawState
        {
            get { return m_DrawState; }
            set
            {
                m_DrawState = value;
                Dirty(ModificationScope.Layout);
            }
        }

        public virtual bool canSetPrecision
        {
            get { return true; }
        }

        private ConcretePrecision m_ConcretePrecision = ConcretePrecision.Single;

        public ConcretePrecision concretePrecision
        {
            get => m_ConcretePrecision;
            set => m_ConcretePrecision = value;
        }

        [SerializeField]
        private Precision m_Precision = Precision.Inherit;

        public Precision precision
        {
            get => m_Precision;
            set => m_Precision = value;
        }

        [SerializeField]
        bool m_PreviewExpanded = true;

        public bool previewExpanded
        {
            get { return m_PreviewExpanded; }
            set
            {
                if (previewExpanded == value)
                    return;
                m_PreviewExpanded = value;
                Dirty(ModificationScope.Node);
            }
        }

        // Nodes that want to have a preview area can override this and return true
        public virtual bool hasPreview
        {
            get { return false; }
        }

        public virtual PreviewMode previewMode
        {
            get { return PreviewMode.Preview2D; }
        }

        public virtual bool allowedInSubGraph
        {
            get { return !(this is BlockNode); }
        }

        public virtual bool allowedInMainGraph
        {
            get { return true; }
        }

        public virtual bool allowedInLayerGraph
        {
            get { return true; }
        }

        public virtual bool hasError
        {
            get { return m_HasError; }
            protected set { m_HasError = value; }
        }

        public virtual bool isActive
        {
            get { return m_IsActive; }
        }

        //There are times when isActive needs to be set to a value explicitly, and
        //not be changed by active forest parsing (what we do when we need to figure out
        //what nodes should or should not be active, usually from an edit; see NodeUtils).
        //In this case, we allow for explicit setting of an active value that cant be overriden.
        //Implicit implies that active forest parsing can edit the nodes isActive property
        public enum ActiveState
        {
            Implicit = 0,
            ExplicitInactive = 1,
            ExplicitActive = 2
        }

        private ActiveState m_ActiveState = ActiveState.Implicit;
        public ActiveState activeState
        {
            get => m_ActiveState;
        }

        public void SetOverrideActiveState(ActiveState overrideState, bool updateConnections = true)
        {
            if (m_ActiveState == overrideState)
            {
                return;
            }

            m_ActiveState = overrideState;
            switch (m_ActiveState)
            {
                case ActiveState.Implicit:
                    if (updateConnections)
                    {
                        NodeUtils.ReevaluateActivityOfConnectedNodes(this);
                    }
                    break;
                case ActiveState.ExplicitInactive:
                    if (m_IsActive == false)
                    {
                        break;
                    }
                    else
                    {
                        m_IsActive = false;
                        Dirty(ModificationScope.Node);
                        if (updateConnections)
                        {
                            NodeUtils.ReevaluateActivityOfConnectedNodes(this);
                        }
                        break;
                    }
                case ActiveState.ExplicitActive:
                    if (m_IsActive == true)
                    {
                        break;
                    }
                    else
                    {
                        m_IsActive = true;
                        Dirty(ModificationScope.Node);
                        if (updateConnections)
                        {
                            NodeUtils.ReevaluateActivityOfConnectedNodes(this);
                        }
                        break;
                    }
            }
        }

        public void SetActive(bool value, bool updateConnections = true)
        {
            if (m_IsActive == value)
                return;

            if (m_ActiveState != ActiveState.Implicit)
            {
                Debug.LogError($"Cannot set IsActive on Node {this} when value is explicitly overriden by ActiveState {m_ActiveState}");
                return;
            }

            // Update this node
            m_IsActive = value;
            Dirty(ModificationScope.Node);

            if (updateConnections)
            {
                NodeUtils.ReevaluateActivityOfConnectedNodes(this);
            }
        }

        public virtual bool isValid
        {
            get { return m_IsValid; }
            set
            {
                if (m_IsValid == value)
                    return;

                m_IsValid = value;
            }
        }


        string m_DefaultVariableName;
        string m_NameForDefaultVariableName;

        string defaultVariableName
        {
            get
            {
                if (m_NameForDefaultVariableName != name)
                {
                    m_DefaultVariableName = string.Format("{0}_{1}", NodeUtils.GetHLSLSafeName(name ?? "node"), objectId);
                    m_NameForDefaultVariableName = name;
                }
                return m_DefaultVariableName;
            }
        }

        #region Custom Colors

        [SerializeField]
        CustomColorData m_CustomColors = new CustomColorData();

        public bool TryGetColor(string provider, ref Color color)
        {
            return m_CustomColors.TryGetColor(provider, out color);
        }

        public void ResetColor(string provider)
        {
            m_CustomColors.Remove(provider);
        }

        public void SetColor(string provider, Color color)
        {
            m_CustomColors.Set(provider, color);
        }

        #endregion

        protected AbstractMaterialNode()
        {
            m_DrawState.expanded = true;
        }

        public void GetInputSlots<T>(List<T> foundSlots) where T : MaterialSlot
        {
            foreach (var slot in m_Slots.SelectValue())
            {
                if (slot.isInputSlot && slot is T)
                    foundSlots.Add((T)slot);
            }
        }

        public void GetOutputSlots<T>(List<T> foundSlots) where T : MaterialSlot
        {
            foreach (var slot in m_Slots.SelectValue())
            {
                if (slot.isOutputSlot && slot is T materialSlot)
                {
                    foundSlots.Add(materialSlot);
                }
            }
        }

        public void GetSlots<T>(List<T> foundSlots) where T : MaterialSlot
        {
            foreach (var slot in m_Slots.SelectValue())
            {
                if (slot is T materialSlot)
                {
                    foundSlots.Add(materialSlot);
                }
            }
        }

        public virtual void CollectShaderProperties(PropertyCollector properties, GenerationMode generationMode)
        {
            foreach (var inputSlot in this.GetInputSlots<MaterialSlot>())
            {
                var edges = owner.GetEdges(inputSlot.slotReference);
                if (edges.Any(e => e.outputSlot.node.isActive))
                    continue;

                inputSlot.AddDefaultProperty(properties, generationMode);
            }
        }

        public string GetSlotValue(int inputSlotId, GenerationMode generationMode, ConcretePrecision concretePrecision)
        {
            string slotValue = GetSlotValue(inputSlotId, generationMode);
            return slotValue.Replace(PrecisionUtil.Token, concretePrecision.ToShaderString());
        }

        public string GetSlotValue(int inputSlotId, GenerationMode generationMode)
        {
            var inputSlot = FindSlot<MaterialSlot>(inputSlotId);
            if (inputSlot == null)
                return string.Empty;

            var edges = owner.GetEdges(inputSlot.slotReference);

            if (edges.Any())
            {
                var fromSocketRef = edges.First().outputSlot;
                var fromNode = fromSocketRef.node;
                return fromNode.GetOutputForSlot(fromSocketRef, inputSlot.concreteValueType, generationMode);
            }

            return inputSlot.GetDefaultValue(generationMode);
        }

        public AbstractShaderProperty GetSlotProperty(int inputSlotId)
        {
            if (owner == null)
                return null;

            var inputSlot = FindSlot<MaterialSlot>(inputSlotId);
            if (inputSlot?.slotReference.node == null)
                return null;

            var edges = owner.GetEdges(inputSlot.slotReference);
            if (edges.Any())
            {
                var fromSocketRef = edges.First().outputSlot;
                var fromNode = fromSocketRef.node;
                if (fromNode == null)
                    return null;        // this is an error condition... we have an edge that connects to a non-existant node?

                if (fromNode is PropertyNode propNode)
                {
                    return propNode.property;
                }

                if (fromNode is RedirectNodeData redirectNode)
                {
                    return redirectNode.GetSlotProperty(RedirectNodeData.kInputSlotID);
                }

#if PROCEDURAL_VT_IN_GRAPH
                if (fromNode is ProceduralVirtualTextureNode pvtNode)
                {
                    return pvtNode.AsShaderProperty();
                }
#endif // PROCEDURAL_VT_IN_GRAPH

                return null;
            }

            return null;
        }

        protected internal virtual string GetOutputForSlot(SlotReference fromSocketRef,  ConcreteSlotValueType valueType, GenerationMode generationMode)
        {
            var slot = FindOutputSlot<MaterialSlot>(fromSocketRef.slotId);
            if (slot == null)
                return string.Empty;

            if (fromSocketRef.node.isActive)
                return GenerationUtils.AdaptNodeOutput(this, slot.id, valueType);
            else
                return slot.GetDefaultValue(generationMode);
        }

        public AbstractMaterialNode GetInputNodeFromSlot(int inputSlotId)
        {
            var inputSlot = FindSlot<MaterialSlot>(inputSlotId);
            if (inputSlot == null)
                return null;

            var edges = owner.GetEdges(inputSlot.slotReference).ToArray();
            var fromSocketRef = edges[0].outputSlot;
            var fromNode = fromSocketRef.node;
            return fromNode;
        }

        public static ConcreteSlotValueType ConvertDynamicVectorInputTypeToConcrete(IEnumerable<ConcreteSlotValueType> inputTypes)
        {
            var concreteSlotValueTypes = inputTypes as IList<ConcreteSlotValueType> ?? inputTypes.ToList();

            var inputTypesDistinct = concreteSlotValueTypes.Distinct().ToList();
            switch (inputTypesDistinct.Count)
            {
                case 0:
                    return ConcreteSlotValueType.Vector1;
                case 1:
                    if (SlotValueHelper.AreCompatible(SlotValueType.DynamicVector, inputTypesDistinct.First()))
                        return inputTypesDistinct.First();
                    break;
                default:
                    // find the 'minumum' channel width excluding 1 as it can promote
                    inputTypesDistinct.RemoveAll(x => x == ConcreteSlotValueType.Vector1);
                    var ordered = inputTypesDistinct.OrderByDescending(x => x);
                    if (ordered.Any())
                        return ordered.FirstOrDefault();
                    break;
            }
            return ConcreteSlotValueType.Vector1;
        }

        public static ConcreteSlotValueType ConvertDynamicMatrixInputTypeToConcrete(IEnumerable<ConcreteSlotValueType> inputTypes)
        {
            var concreteSlotValueTypes = inputTypes as IList<ConcreteSlotValueType> ?? inputTypes.ToList();

            var inputTypesDistinct = concreteSlotValueTypes.Distinct().ToList();
            switch (inputTypesDistinct.Count)
            {
                case 0:
                    return ConcreteSlotValueType.Matrix2;
                case 1:
                    return inputTypesDistinct.FirstOrDefault();
                default:
                    var ordered = inputTypesDistinct.OrderByDescending(x => x);
                    if (ordered.Any())
                        return ordered.FirstOrDefault();
                    break;
            }
            return ConcreteSlotValueType.Matrix2;
        }

        protected const string k_validationErrorMessage = "Error found during node validation";

        public virtual void EvaluateConcretePrecision()
        {
            // If Node has a precision override use that
            if (precision != Precision.Inherit)
            {
                m_ConcretePrecision = precision.ToConcrete();
                return;
            }

            // Get inputs
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);

                // If no inputs were found use the precision of the Graph
                // This can be removed when parameters are considered as true inputs
                if (tempSlots.Count == 0)
                {
                    m_ConcretePrecision = owner.concretePrecision;
                    return;
                }

                // Otherwise compare precisions from inputs
                var precisionsToCompare = new List<int>();

                foreach (var inputSlot in tempSlots)
                {
                    // If input port doesnt have an edge use the Graph's precision for that input
                    var edges = owner?.GetEdges(inputSlot.slotReference).ToList();
                    if (!edges.Any())
                    {
                        precisionsToCompare.Add((int)owner.concretePrecision);
                        continue;
                    }

                    // Get output node from edge
                    var outputSlotRef = edges[0].outputSlot;
                    var outputNode = outputSlotRef.node;

                    // Use precision from connected Node
                    precisionsToCompare.Add((int)outputNode.concretePrecision);
                }

                // Use highest precision from all input sources
                m_ConcretePrecision = (ConcretePrecision)precisionsToCompare.OrderBy(x => x).First();

                // Clean up
                return;
            }
        }

        public virtual void EvaluateDynamicMaterialSlots()
        {
            var dynamicInputSlotsToCompare = DictionaryPool<DynamicVectorMaterialSlot, ConcreteSlotValueType>.Get();
            var skippedDynamicSlots = ListPool<DynamicVectorMaterialSlot>.Get();

            var dynamicMatrixInputSlotsToCompare = DictionaryPool<DynamicMatrixMaterialSlot, ConcreteSlotValueType>.Get();
            var skippedDynamicMatrixSlots = ListPool<DynamicMatrixMaterialSlot>.Get();

            // iterate the input slots
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var inputSlot in tempSlots)
                {
                    inputSlot.hasError = false;
                    // if there is a connection
                    var edges = owner.GetEdges(inputSlot.slotReference).ToList();
                    if (!edges.Any())
                    {
                        if (inputSlot is DynamicVectorMaterialSlot)
                            skippedDynamicSlots.Add(inputSlot as DynamicVectorMaterialSlot);
                        if (inputSlot is DynamicMatrixMaterialSlot)
                            skippedDynamicMatrixSlots.Add(inputSlot as DynamicMatrixMaterialSlot);
                        continue;
                    }

                    // get the output details
                    var outputSlotRef = edges[0].outputSlot;
                    var outputNode = outputSlotRef.node;
                    if (outputNode == null)
                        continue;

                    var outputSlot = outputNode.FindOutputSlot<MaterialSlot>(outputSlotRef.slotId);
                    if (outputSlot == null)
                        continue;

                    if (outputSlot.hasError)
                    {
                        inputSlot.hasError = true;
                        continue;
                    }

                    var outputConcreteType = outputSlot.concreteValueType;
                    // dynamic input... depends on output from other node.
                    // we need to compare ALL dynamic inputs to make sure they
                    // are compatible.
                    if (inputSlot is DynamicVectorMaterialSlot)
                    {
                        dynamicInputSlotsToCompare.Add((DynamicVectorMaterialSlot)inputSlot, outputConcreteType);
                        continue;
                    }
                    else if (inputSlot is DynamicMatrixMaterialSlot)
                    {
                        dynamicMatrixInputSlotsToCompare.Add((DynamicMatrixMaterialSlot)inputSlot, outputConcreteType);
                        continue;
                    }
                }

                // we can now figure out the dynamic slotType
                // from here set all the
                var dynamicType = ConvertDynamicVectorInputTypeToConcrete(dynamicInputSlotsToCompare.Values);
                foreach (var dynamicKvP in dynamicInputSlotsToCompare)
                    dynamicKvP.Key.SetConcreteType(dynamicType);
                foreach (var skippedSlot in skippedDynamicSlots)
                    skippedSlot.SetConcreteType(dynamicType);

                // and now dynamic matrices
                var dynamicMatrixType = ConvertDynamicMatrixInputTypeToConcrete(dynamicMatrixInputSlotsToCompare.Values);
                foreach (var dynamicKvP in dynamicMatrixInputSlotsToCompare)
                    dynamicKvP.Key.SetConcreteType(dynamicMatrixType);
                foreach (var skippedSlot in skippedDynamicMatrixSlots)
                    skippedSlot.SetConcreteType(dynamicMatrixType);

                tempSlots.Clear();
                GetInputSlots(tempSlots);
                bool inputError = tempSlots.Any(x => x.hasError);
                if (inputError)
                {
                    owner.AddConcretizationError(objectId, string.Format("Node {0} had input error", objectId));
                    hasError = true;
                }

                // configure the output slots now
                // their slotType will either be the default output slotType
                // or the above dynamic slotType for dynamic nodes
                // or error if there is an input error
                tempSlots.Clear();
                GetOutputSlots(tempSlots);
                foreach (var outputSlot in tempSlots)
                {
                    outputSlot.hasError = false;

                    if (inputError)
                    {
                        outputSlot.hasError = true;
                        continue;
                    }

                    if (outputSlot is DynamicVectorMaterialSlot dynamicVectorMaterialSlot)
                    {
                        dynamicVectorMaterialSlot.SetConcreteType(dynamicType);
                        continue;
                    }
                    else if (outputSlot is DynamicMatrixMaterialSlot dynamicMatrixMaterialSlot)
                    {
                        dynamicMatrixMaterialSlot.SetConcreteType(dynamicMatrixType);
                        continue;
                    }
                }


                tempSlots.Clear();
                GetOutputSlots(tempSlots);
                if (tempSlots.Any(x => x.hasError))
                {
                    owner.AddConcretizationError(objectId, string.Format("Node {0} had output error", objectId));
                    hasError = true;
                }
                CalculateNodeHasError();

                ListPool<DynamicVectorMaterialSlot>.Release(skippedDynamicSlots);
                DictionaryPool<DynamicVectorMaterialSlot, ConcreteSlotValueType>.Release(dynamicInputSlotsToCompare);

                ListPool<DynamicMatrixMaterialSlot>.Release(skippedDynamicMatrixSlots);
                DictionaryPool<DynamicMatrixMaterialSlot, ConcreteSlotValueType>.Release(dynamicMatrixInputSlotsToCompare);
            }
        }

        public virtual void Concretize()
        {
            hasError = false;
            owner?.ClearErrorsForNode(this);
            EvaluateConcretePrecision();
            EvaluateDynamicMaterialSlots();
        }

        public virtual void ValidateNode()
        {
        }

        public virtual bool canCutNode => true;
        public virtual bool canCopyNode => true;

        protected virtual void CalculateNodeHasError()
        {
            foreach (var slot in this.GetInputSlots<MaterialSlot>())
            {
                if (slot.isConnected)
                {
                    var edge = owner.GetEdges(slot.slotReference).First();
                    var outputNode = edge.outputSlot.node;
                    var outputSlot = outputNode.GetOutputSlots<MaterialSlot>().First(s => s.id == edge.outputSlot.slotId);
                    if (!slot.IsCompatibleWith(outputSlot))
                    {
                        owner.AddConcretizationError(objectId, $"Slot {slot.RawDisplayName()} cannot accept input of type {outputSlot.concreteValueType}.");
                        hasError = true;
                        return;
                    }
                }
            }
        }

        public virtual void CollectPreviewMaterialProperties(List<PreviewProperty> properties)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            using (var tempPreviewProperties = PooledList<PreviewProperty>.Get())
            using (var tempEdges = PooledList<IEdge>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var s in tempSlots)
                {
                    tempPreviewProperties.Clear();
                    tempEdges.Clear();
                    owner.GetEdges(s.slotReference, tempEdges);
                    if (tempEdges.Any())
                        continue;

                    s.GetPreviewProperties(tempPreviewProperties, GetVariableNameForSlot(s.id));
                    for (int i = 0; i < tempPreviewProperties.Count; i++)
                    {
                        if (tempPreviewProperties[i].name == null)
                            continue;

                        properties.Add(tempPreviewProperties[i]);
                    }
                }
            }
        }

        public virtual string GetVariableNameForSlot(int slotId)
        {
            var slot = FindSlot<MaterialSlot>(slotId);
            if (slot == null)
                throw new ArgumentException(string.Format("Attempting to use MaterialSlot({0}) on node of type {1} where this slot can not be found", slotId, this), "slotId");
            return string.Format("_{0}_{1}_{2}", GetVariableNameForNode(), NodeUtils.GetHLSLSafeName(slot.shaderOutputName), unchecked((uint)slotId));
        }

        public virtual string GetVariableNameForNode()
        {
            return defaultVariableName;
        }

        public MaterialSlot AddSlot(MaterialSlot slot, bool attemptToModifyExistingInstance = true)
        {
            if (slot == null)
            {
                throw new ArgumentException($"Trying to add null slot to node {this}");
            }
            MaterialSlot foundSlot = FindSlot<MaterialSlot>(slot.id);

            if (slot == foundSlot)
                return foundSlot;

            // Try to keep the existing instance to avoid unnecessary changes to file
            if (attemptToModifyExistingInstance && foundSlot != null && slot.GetType() == foundSlot.GetType())
            {
                foundSlot.displayName = slot.RawDisplayName();
                foundSlot.CopyDefaultValue(slot);
                return foundSlot;
            }

            // keep the same ordering by replacing the first match, if it exists
            int firstIndex = m_Slots.FindIndex(s => s.value.id == slot.id);
            if (firstIndex >= 0)
            {
                m_Slots[firstIndex] = slot;

                // remove additional matches to get rid of unused duplicates
                m_Slots.RemoveAllFromRange(s => s.value.id == slot.id, firstIndex + 1, m_Slots.Count - (firstIndex + 1));
            }
            else
                m_Slots.Add(slot);

            slot.owner = this;

            OnSlotsChanged();

            if (foundSlot == null)
                return slot;

            // foundSlot is of a different type; try to copy values
            // I think this is to support casting if implemented in CopyValuesFrom ?
            slot.CopyValuesFrom(foundSlot);
            foundSlot.owner = null;

            return slot;
        }

        public void RemoveSlot(int slotId)
        {
            // Remove edges that use this slot
            // no owner can happen after creation
            // but before added to graph
            if (owner != null)
            {
                var edges = owner.GetEdges(GetSlotReference(slotId));

                foreach (var edge in edges.ToArray())
                    owner.RemoveEdge(edge);
            }

            //remove slots
            m_Slots.RemoveAll(x => x.value.id == slotId);

            OnSlotsChanged();
        }

        protected virtual void OnSlotsChanged()
        {
            Dirty(ModificationScope.Topological);
            owner?.ClearErrorsForNode(this);
        }

        public void RemoveSlotsNameNotMatching(IEnumerable<int> slotIds, bool supressWarnings = false)
        {
            var invalidSlots = m_Slots.Select(x => x.value.id).Except(slotIds);

            foreach (var invalidSlot in invalidSlots.ToArray())
            {
                if (!supressWarnings)
                    Debug.LogWarningFormat("Removing Invalid MaterialSlot: {0}", invalidSlot);
                RemoveSlot(invalidSlot);
            }
        }

        public void SetSlotOrder(List<int> desiredOrderSlotIds)
        {
            int writeIndex = 0;
            for (int orderIndex = 0; orderIndex < desiredOrderSlotIds.Count; orderIndex++)
            {
                var id = desiredOrderSlotIds[orderIndex];
                var matchIndex = m_Slots.FindIndex(s => s.value.id == id);
                if (matchIndex < 0)
                {
                    // no matching slot with that id.. skip it
                }
                else
                {
                    if (writeIndex != matchIndex)
                    {
                        // swap the matching slot into position
                        var slot = m_Slots[matchIndex];
                        m_Slots[matchIndex] = m_Slots[writeIndex];
                        m_Slots[writeIndex] = slot;
                    }
                    writeIndex++;
                }
            }
        }

        public SlotReference GetSlotReference(int slotId)
        {
            var slot = FindSlot<MaterialSlot>(slotId);
            if (slot == null)
                throw new ArgumentException("Slot could not be found", "slotId");
            return new SlotReference(this, slotId);
        }

        public T FindSlot<T>(int slotId) where T : MaterialSlot
        {
            foreach (var slot in m_Slots.SelectValue())
            {
                if (slot.id == slotId && slot is T)
                    return (T)slot;
            }
            return default(T);
        }

        public T FindInputSlot<T>(int slotId) where T : MaterialSlot
        {
            foreach (var slot in m_Slots.SelectValue())
            {
                if (slot.isInputSlot && slot.id == slotId && slot is T)
                    return (T)slot;
            }
            return default(T);
        }

        public T FindOutputSlot<T>(int slotId) where T : MaterialSlot
        {
            foreach (var slot in m_Slots.SelectValue())
            {
                if (slot.isOutputSlot && slot.id == slotId && slot is T)
                    return (T)slot;
            }
            return default(T);
        }

        public virtual IEnumerable<MaterialSlot> GetInputsWithNoConnection()
        {
            return this.GetInputSlots<MaterialSlot>().Where(x => !owner.GetEdges(GetSlotReference(x.id)).Any());
        }

        public void SetupSlots()
        {
            foreach (var s in m_Slots.SelectValue())
                s.owner = this;
        }

        public virtual void UpdateNodeAfterDeserialization()
        {}

        public bool IsSlotConnected(int slotId)
        {
            var slot = FindSlot<MaterialSlot>(slotId);
            return slot != null && owner.GetEdges(slot.slotReference).Any();
        }

        public virtual void Setup() {}

        protected void EnqueSlotsForSerialization()
        {
            foreach (var slot in m_Slots)
            {
                slot.OnBeforeSerialize();
            }
        }
    }
}
