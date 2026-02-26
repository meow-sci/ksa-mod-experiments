```
═══════════ blinken DEBUG DUMP: pixel_0_0_a ═══════════
part.GetType()          = KSA.Part
part.Id                 = pixel_0_0_a
part.DisplayName        = pixel_0_0_a
part.IsSubPart          = False
part.PartParent?.Id     = (null)
part.Components  (2 type(s)):
  [KSA.InertMass]  count=1
      part.Components[InertMass][0].GetType()  = KSA.InertMass ␦ KSA.PartComponent`1[[KSA.InertMass, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.Components[InertMass][0].MassPropertiesAsmb  = KSA.OffsetMassProperties  (OffsetMassProperties)
      part.Components[InertMass][0].Parent  = KSA.Part  (Part)
      part.Components[InertMass][0].InstanceId  = 1333  (UInt64)
      part.Components[InertMass][0].TemplateId  =   (String)
  [KSA.EngineController]  count=1
      part.Components[EngineController][0].GetType()  = KSA.EngineController ␦ KSA.PartComponentStateful`4[[KSA.EngineController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EngineControllerState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.EngineController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.Components[EngineController][0] implements: IActivate
      part.Components[EngineController][0].IsActive  = True  (Boolean)
      part.Components[EngineController][0].Cores  = [1 items] RocketCore[]  (RocketCore[])
      part.Components[EngineController][0].MinimumThrottle  = 0.01  (Single)
      part.Components[EngineController][0].VacuumData  = KSA.RocketControllerData  (RocketControllerData)
      part.Components[EngineController][0].StatesIdx  = 1  (Int32)
      part.Components[EngineController][0].Parent  = KSA.Part  (Part)
      part.Components[EngineController][0].InstanceId  = 1334  (UInt64)
      part.Components[EngineController][0].TemplateId  = LR91-AJ-3  (String)
      part.Components[EngineController][0].CreateStates(EngineControllerState& state, EmptyStruct& fxState)  ␦ Void
      part.Components[EngineController][0].SetIsActive(Vehicle _, Boolean activationState)  ␦ Void
      part.Components[EngineController][0].GetSaveData(UInt32& localRunningId, PartComponentStateList states)  ␦ SaveDataBase
      part.Components[EngineController][0].ApplySaveData(SaveDataBase saveData, PartComponentStateList states)  ␦ Void
part.SubtreeComponents  (8 type(s)):
  [KSA.GimbalController]  count=1
      part.SubtreeComponents[GimbalController][0].GetType()  = KSA.GimbalController ␦ KSA.PartComponentStateful`4[[KSA.GimbalController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.GimbalControllerState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.GimbalController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[GimbalController][0].Gimbal  = KSA.Gimbal  (Gimbal)
      part.SubtreeComponents[GimbalController][0].Data  = KSA.GimbalControllerData  (GimbalControllerData)
      part.SubtreeComponents[GimbalController][0].StatesIdx  = 5  (Int32)
      part.SubtreeComponents[GimbalController][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[GimbalController][0].InstanceId  = 1325  (UInt64)
      part.SubtreeComponents[GimbalController][0].TemplateId  =   (String)
      part.SubtreeComponents[GimbalController][0].CreateStates(GimbalControllerState& state, EmptyStruct& fxState)  ␦ Void
      part.SubtreeComponents[GimbalController][0].RecomputeStaticData()  ␦ Void
      part.SubtreeComponents[GimbalController][0].RecomputeDynamicData(GimbalControllerState& state, ReadOnlySpan`1 nozzleStates)  ␦ Void
  [KSA.Gimbal]  count=1
      part.SubtreeComponents[Gimbal][0].GetType()  = KSA.Gimbal ␦ KSA.PartComponentStateful`4[[KSA.Gimbal, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.GimbalState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.Gimbal, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[Gimbal][0].PositionAsmb  = <0, 0, 0>  (double3)
      part.SubtreeComponents[Gimbal][0].Gimbal2Asmb  = <0, 0, 0, 1>  (doubleQuat)
      part.SubtreeComponents[Gimbal][0].AxisY  = KSA.GimbalAxis  (GimbalAxis)
      part.SubtreeComponents[Gimbal][0].AxisZ  = KSA.GimbalAxis  (GimbalAxis)
      part.SubtreeComponents[Gimbal][0].ConstrainToCircle  = False  (Boolean)
      part.SubtreeComponents[Gimbal][0].Controller  = KSA.GimbalController  (GimbalController)
      part.SubtreeComponents[Gimbal][0].StatesIdx  = 5  (Int32)
      part.SubtreeComponents[Gimbal][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[Gimbal][0].InstanceId  = 1324  (UInt64)
      part.SubtreeComponents[Gimbal][0].TemplateId  =   (String)
      part.SubtreeComponents[Gimbal][0].CanActuate()  ␦ Boolean
      part.SubtreeComponents[Gimbal][0].CreateStates(GimbalState& state, EmptyStruct& fxState)  ␦ Void
  [KSA.RocketCore]  count=1
      part.SubtreeComponents[RocketCore][0].GetType()  = KSA.Combustor ␦ KSA.RocketCore ␦ KSA.PartComponentStateful`4[[KSA.RocketCore, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketCoreState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.RocketCore, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[RocketCore][0].Config  = KSA.CombustorConfig  (CombustorConfig)
      part.SubtreeComponents[RocketCore][0].MinimumPulseTime  = 0.001  (Single)
      part.SubtreeComponents[RocketCore][0].MinimumThrottle  = 0.01  (Single)
      part.SubtreeComponents[RocketCore][0].Combustion  = KSA.CombustionProcess  (CombustionProcess)
      part.SubtreeComponents[RocketCore][0].ResourceManager  = KSA.ResourceManager  (ResourceManager)
      part.SubtreeComponents[RocketCore][0].Rocket  = KSA.Rocket  (Rocket)
      part.SubtreeComponents[RocketCore][0].Controller  = KSA.EngineController  (IActivate)
      part.SubtreeComponents[RocketCore][0].StatesIdx  = 34  (Int32)
      part.SubtreeComponents[RocketCore][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[RocketCore][0].InstanceId  = 1326  (UInt64)
      part.SubtreeComponents[RocketCore][0].TemplateId  = ThrustChamber  (String)
      part.SubtreeComponents[RocketCore][0].ComputeConditions(Single throttle)  ␦ RocketCoreConditions
  [KSA.RocketNozzle]  count=1
      part.SubtreeComponents[RocketNozzle][0].GetType()  = KSA.DeLavalNozzle ␦ KSA.RocketNozzle ␦ KSA.PartComponentStateful`4[[KSA.RocketNozzle, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketNozzleState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketNozzleFxState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.RocketNozzle, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[RocketNozzle][0].Config  = KSA.DeLavalNozzleConfig  (DeLavalNozzleConfig)
      part.SubtreeComponents[RocketNozzle][0].LocationAsmb  = <-1.155106, 0, 0>  (float3)
      part.SubtreeComponents[RocketNozzle][0].ExhaustDirectionAsmb  = <-1, 0, 0>  (float3)
      part.SubtreeComponents[RocketNozzle][0].FxLocationAsmb  = <-1.155106, 0, 0>  (float3)
      part.SubtreeComponents[RocketNozzle][0].FxExhaustDirectionAsmb  = <-1, 0, 0>  (float3)
      part.SubtreeComponents[RocketNozzle][0].SoundEvent  = KSA.RocketSoundEvent  (RocketSoundEvent)
      part.SubtreeComponents[RocketNozzle][0].VolumetricExhaust  = KSA.VolumetricExhaustReference  (VolumetricExhaustReference)
      part.SubtreeComponents[RocketNozzle][0].ExhaustLight  = True  (Boolean)
      part.SubtreeComponents[RocketNozzle][0].Rocket  = KSA.Rocket  (Rocket)
      part.SubtreeComponents[RocketNozzle][0].MaxExhaustDensity  = 0.0016401502  (Single)
      part.SubtreeComponents[RocketNozzle][0].MaxExhaustTemperature  = 1031.1693  (Single)
      part.SubtreeComponents[RocketNozzle][0].StatesIdx  = 38  (Int32)
      part.SubtreeComponents[RocketNozzle][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[RocketNozzle][0].InstanceId  = 1327  (UInt64)
      part.SubtreeComponents[RocketNozzle][0].TemplateId  = Nozzle  (String)
      part.SubtreeComponents[RocketNozzle][0].ComputePerformance(RocketCoreConditions& coreConditions, Single ambientPressure)  ␦ NozzlePerformance
  [KSA.Rocket]  count=1
      part.SubtreeComponents[Rocket][0].GetType()  = KSA.Rocket ␦ KSA.PartComponent`1[[KSA.Rocket, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[Rocket][0].Core  = KSA.Combustor  (RocketCore)
      part.SubtreeComponents[Rocket][0].Nozzles  = [1 items] RocketNozzle[]  (RocketNozzle[])
      part.SubtreeComponents[Rocket][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[Rocket][0].InstanceId  = 1328  (UInt64)
      part.SubtreeComponents[Rocket][0].TemplateId  = Engine  (String)
  [KSA.FxTemperature]  count=1
      part.SubtreeComponents[FxTemperature][0].GetType()  = KSA.FxTemperature ␦ KSA.PartComponentStateful`4[[KSA.FxTemperature, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.FxTemperatureState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.FxTemperature, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[FxTemperature][0].StatesIdx  = 6  (Int32)
      part.SubtreeComponents[FxTemperature][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[FxTemperature][0].InstanceId  = 1329  (UInt64)
      part.SubtreeComponents[FxTemperature][0].TemplateId  =   (String)
      part.SubtreeComponents[FxTemperature][0].CreateStates(FxTemperatureState& state, EmptyStruct& fxState)  ␦ Void
  [KSA.InertMass]  count=1
      part.SubtreeComponents[InertMass][0].GetType()  = KSA.InertMass ␦ KSA.PartComponent`1[[KSA.InertMass, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[InertMass][0].MassPropertiesAsmb  = KSA.OffsetMassProperties  (OffsetMassProperties)
      part.SubtreeComponents[InertMass][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[InertMass][0].InstanceId  = 1333  (UInt64)
      part.SubtreeComponents[InertMass][0].TemplateId  =   (String)
  [KSA.EngineController]  count=1
      part.SubtreeComponents[EngineController][0].GetType()  = KSA.EngineController ␦ KSA.PartComponentStateful`4[[KSA.EngineController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EngineControllerState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.EngineController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
      part.SubtreeComponents[EngineController][0] implements: IActivate
      part.SubtreeComponents[EngineController][0].IsActive  = True  (Boolean)
      part.SubtreeComponents[EngineController][0].Cores  = [1 items] RocketCore[]  (RocketCore[])
      part.SubtreeComponents[EngineController][0].MinimumThrottle  = 0.01  (Single)
      part.SubtreeComponents[EngineController][0].VacuumData  = KSA.RocketControllerData  (RocketControllerData)
      part.SubtreeComponents[EngineController][0].StatesIdx  = 1  (Int32)
      part.SubtreeComponents[EngineController][0].Parent  = KSA.Part  (Part)
      part.SubtreeComponents[EngineController][0].InstanceId  = 1334  (UInt64)
      part.SubtreeComponents[EngineController][0].TemplateId  = LR91-AJ-3  (String)
      part.SubtreeComponents[EngineController][0].CreateStates(EngineControllerState& state, EmptyStruct& fxState)  ␦ Void
      part.SubtreeComponents[EngineController][0].SetIsActive(Vehicle _, Boolean activationState)  ␦ Void
      part.SubtreeComponents[EngineController][0].GetSaveData(UInt32& localRunningId, PartComponentStateList states)  ␦ SaveDataBase
      part.SubtreeComponents[EngineController][0].ApplySaveData(SaveDataBase saveData, PartComponentStateList states)  ␦ Void
part.SubParts[0] ───────────────────
  part.SubParts[0].GetType()          = KSA.Part
  part.SubParts[0].Id                 = CorePropulsionA_Subpart_EngineA1WBaseCompact1
  part.SubParts[0].DisplayName        = CorePropulsionA_Subpart_EngineA1WBaseCompact1
  part.SubParts[0].IsSubPart          = True
  part.SubParts[0].PartParent?.Id     = pixel_0_0_a
  part.SubParts[0].Components  = (empty - 0 types)
  part.SubParts[0].SubtreeComponents  = (empty - 0 types)
  part.SubParts[0].SubParts = (none)
  part.SubParts[0].TreeChildren = (none)
part.SubParts[1] ───────────────────
  part.SubParts[1].GetType()          = KSA.Part
  part.SubParts[1].Id                 = CorePropulsionA_Subpart_EngineACompactVacAssembly1
  part.SubParts[1].DisplayName        = CorePropulsionA_Subpart_EngineACompactVacAssembly1
  part.SubParts[1].IsSubPart          = True
  part.SubParts[1].PartParent?.Id     = pixel_0_0_a
  part.SubParts[1].Components  (6 type(s)):
    [KSA.GimbalController]  count=1
        part.SubParts[1].Components[GimbalController][0].GetType()  = KSA.GimbalController ␦ KSA.PartComponentStateful`4[[KSA.GimbalController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.GimbalControllerState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.GimbalController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].Components[GimbalController][0].Gimbal  = KSA.Gimbal  (Gimbal)
        part.SubParts[1].Components[GimbalController][0].Data  = KSA.GimbalControllerData  (GimbalControllerData)
        part.SubParts[1].Components[GimbalController][0].StatesIdx  = 5  (Int32)
        part.SubParts[1].Components[GimbalController][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].Components[GimbalController][0].InstanceId  = 1325  (UInt64)
        part.SubParts[1].Components[GimbalController][0].TemplateId  =   (String)
        part.SubParts[1].Components[GimbalController][0].CreateStates(GimbalControllerState& state, EmptyStruct& fxState)  ␦ Void
        part.SubParts[1].Components[GimbalController][0].RecomputeStaticData()  ␦ Void
        part.SubParts[1].Components[GimbalController][0].RecomputeDynamicData(GimbalControllerState& state, ReadOnlySpan`1 nozzleStates)  ␦ Void
    [KSA.Gimbal]  count=1
        part.SubParts[1].Components[Gimbal][0].GetType()  = KSA.Gimbal ␦ KSA.PartComponentStateful`4[[KSA.Gimbal, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.GimbalState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.Gimbal, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].Components[Gimbal][0].PositionAsmb  = <0, 0, 0>  (double3)
        part.SubParts[1].Components[Gimbal][0].Gimbal2Asmb  = <0, 0, 0, 1>  (doubleQuat)
        part.SubParts[1].Components[Gimbal][0].AxisY  = KSA.GimbalAxis  (GimbalAxis)
        part.SubParts[1].Components[Gimbal][0].AxisZ  = KSA.GimbalAxis  (GimbalAxis)
        part.SubParts[1].Components[Gimbal][0].ConstrainToCircle  = False  (Boolean)
        part.SubParts[1].Components[Gimbal][0].Controller  = KSA.GimbalController  (GimbalController)
        part.SubParts[1].Components[Gimbal][0].StatesIdx  = 5  (Int32)
        part.SubParts[1].Components[Gimbal][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].Components[Gimbal][0].InstanceId  = 1324  (UInt64)
        part.SubParts[1].Components[Gimbal][0].TemplateId  =   (String)
        part.SubParts[1].Components[Gimbal][0].CanActuate()  ␦ Boolean
        part.SubParts[1].Components[Gimbal][0].CreateStates(GimbalState& state, EmptyStruct& fxState)  ␦ Void
    [KSA.RocketCore]  count=1
        part.SubParts[1].Components[RocketCore][0].GetType()  = KSA.Combustor ␦ KSA.RocketCore ␦ KSA.PartComponentStateful`4[[KSA.RocketCore, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketCoreState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.RocketCore, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].Components[RocketCore][0].Config  = KSA.CombustorConfig  (CombustorConfig)
        part.SubParts[1].Components[RocketCore][0].MinimumPulseTime  = 0.001  (Single)
        part.SubParts[1].Components[RocketCore][0].MinimumThrottle  = 0.01  (Single)
        part.SubParts[1].Components[RocketCore][0].Combustion  = KSA.CombustionProcess  (CombustionProcess)
        part.SubParts[1].Components[RocketCore][0].ResourceManager  = KSA.ResourceManager  (ResourceManager)
        part.SubParts[1].Components[RocketCore][0].Rocket  = KSA.Rocket  (Rocket)
        part.SubParts[1].Components[RocketCore][0].Controller  = KSA.EngineController  (IActivate)
        part.SubParts[1].Components[RocketCore][0].StatesIdx  = 34  (Int32)
        part.SubParts[1].Components[RocketCore][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].Components[RocketCore][0].InstanceId  = 1326  (UInt64)
        part.SubParts[1].Components[RocketCore][0].TemplateId  = ThrustChamber  (String)
        part.SubParts[1].Components[RocketCore][0].ComputeConditions(Single throttle)  ␦ RocketCoreConditions
    [KSA.RocketNozzle]  count=1
        part.SubParts[1].Components[RocketNozzle][0].GetType()  = KSA.DeLavalNozzle ␦ KSA.RocketNozzle ␦ KSA.PartComponentStateful`4[[KSA.RocketNozzle, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketNozzleState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketNozzleFxState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.RocketNozzle, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].Components[RocketNozzle][0].Config  = KSA.DeLavalNozzleConfig  (DeLavalNozzleConfig)
        part.SubParts[1].Components[RocketNozzle][0].LocationAsmb  = <-1.155106, 0, 0>  (float3)
        part.SubParts[1].Components[RocketNozzle][0].ExhaustDirectionAsmb  = <-1, 0, 0>  (float3)
        part.SubParts[1].Components[RocketNozzle][0].FxLocationAsmb  = <-1.155106, 0, 0>  (float3)
        part.SubParts[1].Components[RocketNozzle][0].FxExhaustDirectionAsmb  = <-1, 0, 0>  (float3)
        part.SubParts[1].Components[RocketNozzle][0].SoundEvent  = KSA.RocketSoundEvent  (RocketSoundEvent)
        part.SubParts[1].Components[RocketNozzle][0].VolumetricExhaust  = KSA.VolumetricExhaustReference  (VolumetricExhaustReference)
        part.SubParts[1].Components[RocketNozzle][0].ExhaustLight  = True  (Boolean)
        part.SubParts[1].Components[RocketNozzle][0].Rocket  = KSA.Rocket  (Rocket)
        part.SubParts[1].Components[RocketNozzle][0].MaxExhaustDensity  = 0.0016401502  (Single)
        part.SubParts[1].Components[RocketNozzle][0].MaxExhaustTemperature  = 1031.1693  (Single)
        part.SubParts[1].Components[RocketNozzle][0].StatesIdx  = 38  (Int32)
        part.SubParts[1].Components[RocketNozzle][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].Components[RocketNozzle][0].InstanceId  = 1327  (UInt64)
        part.SubParts[1].Components[RocketNozzle][0].TemplateId  = Nozzle  (String)
        part.SubParts[1].Components[RocketNozzle][0].ComputePerformance(RocketCoreConditions& coreConditions, Single ambientPressure)  ␦ NozzlePerformance
    [KSA.Rocket]  count=1
        part.SubParts[1].Components[Rocket][0].GetType()  = KSA.Rocket ␦ KSA.PartComponent`1[[KSA.Rocket, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].Components[Rocket][0].Core  = KSA.Combustor  (RocketCore)
        part.SubParts[1].Components[Rocket][0].Nozzles  = [1 items] RocketNozzle[]  (RocketNozzle[])
        part.SubParts[1].Components[Rocket][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].Components[Rocket][0].InstanceId  = 1328  (UInt64)
        part.SubParts[1].Components[Rocket][0].TemplateId  = Engine  (String)
    [KSA.FxTemperature]  count=1
        part.SubParts[1].Components[FxTemperature][0].GetType()  = KSA.FxTemperature ␦ KSA.PartComponentStateful`4[[KSA.FxTemperature, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.FxTemperatureState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.FxTemperature, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].Components[FxTemperature][0].StatesIdx  = 6  (Int32)
        part.SubParts[1].Components[FxTemperature][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].Components[FxTemperature][0].InstanceId  = 1329  (UInt64)
        part.SubParts[1].Components[FxTemperature][0].TemplateId  =   (String)
        part.SubParts[1].Components[FxTemperature][0].CreateStates(FxTemperatureState& state, EmptyStruct& fxState)  ␦ Void
  part.SubParts[1].SubtreeComponents  (6 type(s)):
    [KSA.GimbalController]  count=1
        part.SubParts[1].SubtreeComponents[GimbalController][0].GetType()  = KSA.GimbalController ␦ KSA.PartComponentStateful`4[[KSA.GimbalController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.GimbalControllerState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.GimbalController, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].SubtreeComponents[GimbalController][0].Gimbal  = KSA.Gimbal  (Gimbal)
        part.SubParts[1].SubtreeComponents[GimbalController][0].Data  = KSA.GimbalControllerData  (GimbalControllerData)
        part.SubParts[1].SubtreeComponents[GimbalController][0].StatesIdx  = 5  (Int32)
        part.SubParts[1].SubtreeComponents[GimbalController][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].SubtreeComponents[GimbalController][0].InstanceId  = 1325  (UInt64)
        part.SubParts[1].SubtreeComponents[GimbalController][0].TemplateId  =   (String)
        part.SubParts[1].SubtreeComponents[GimbalController][0].CreateStates(GimbalControllerState& state, EmptyStruct& fxState)  ␦ Void
        part.SubParts[1].SubtreeComponents[GimbalController][0].RecomputeStaticData()  ␦ Void
        part.SubParts[1].SubtreeComponents[GimbalController][0].RecomputeDynamicData(GimbalControllerState& state, ReadOnlySpan`1 nozzleStates)  ␦ Void
    [KSA.Gimbal]  count=1
        part.SubParts[1].SubtreeComponents[Gimbal][0].GetType()  = KSA.Gimbal ␦ KSA.PartComponentStateful`4[[KSA.Gimbal, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.GimbalState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.Gimbal, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].SubtreeComponents[Gimbal][0].PositionAsmb  = <0, 0, 0>  (double3)
        part.SubParts[1].SubtreeComponents[Gimbal][0].Gimbal2Asmb  = <0, 0, 0, 1>  (doubleQuat)
        part.SubParts[1].SubtreeComponents[Gimbal][0].AxisY  = KSA.GimbalAxis  (GimbalAxis)
        part.SubParts[1].SubtreeComponents[Gimbal][0].AxisZ  = KSA.GimbalAxis  (GimbalAxis)
        part.SubParts[1].SubtreeComponents[Gimbal][0].ConstrainToCircle  = False  (Boolean)
        part.SubParts[1].SubtreeComponents[Gimbal][0].Controller  = KSA.GimbalController  (GimbalController)
        part.SubParts[1].SubtreeComponents[Gimbal][0].StatesIdx  = 5  (Int32)
        part.SubParts[1].SubtreeComponents[Gimbal][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].SubtreeComponents[Gimbal][0].InstanceId  = 1324  (UInt64)
        part.SubParts[1].SubtreeComponents[Gimbal][0].TemplateId  =   (String)
        part.SubParts[1].SubtreeComponents[Gimbal][0].CanActuate()  ␦ Boolean
        part.SubParts[1].SubtreeComponents[Gimbal][0].CreateStates(GimbalState& state, EmptyStruct& fxState)  ␦ Void
    [KSA.RocketCore]  count=1
        part.SubParts[1].SubtreeComponents[RocketCore][0].GetType()  = KSA.Combustor ␦ KSA.RocketCore ␦ KSA.PartComponentStateful`4[[KSA.RocketCore, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketCoreState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.RocketCore, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].SubtreeComponents[RocketCore][0].Config  = KSA.CombustorConfig  (CombustorConfig)
        part.SubParts[1].SubtreeComponents[RocketCore][0].MinimumPulseTime  = 0.001  (Single)
        part.SubParts[1].SubtreeComponents[RocketCore][0].MinimumThrottle  = 0.01  (Single)
        part.SubParts[1].SubtreeComponents[RocketCore][0].Combustion  = KSA.CombustionProcess  (CombustionProcess)
        part.SubParts[1].SubtreeComponents[RocketCore][0].ResourceManager  = KSA.ResourceManager  (ResourceManager)
        part.SubParts[1].SubtreeComponents[RocketCore][0].Rocket  = KSA.Rocket  (Rocket)
        part.SubParts[1].SubtreeComponents[RocketCore][0].Controller  = KSA.EngineController  (IActivate)
        part.SubParts[1].SubtreeComponents[RocketCore][0].StatesIdx  = 34  (Int32)
        part.SubParts[1].SubtreeComponents[RocketCore][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].SubtreeComponents[RocketCore][0].InstanceId  = 1326  (UInt64)
        part.SubParts[1].SubtreeComponents[RocketCore][0].TemplateId  = ThrustChamber  (String)
        part.SubParts[1].SubtreeComponents[RocketCore][0].ComputeConditions(Single throttle)  ␦ RocketCoreConditions
    [KSA.RocketNozzle]  count=1
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].GetType()  = KSA.DeLavalNozzle ␦ KSA.RocketNozzle ␦ KSA.PartComponentStateful`4[[KSA.RocketNozzle, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketNozzleState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.RocketNozzleFxState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.RocketNozzle, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].Config  = KSA.DeLavalNozzleConfig  (DeLavalNozzleConfig)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].LocationAsmb  = <-1.155106, 0, 0>  (float3)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].ExhaustDirectionAsmb  = <-1, 0, 0>  (float3)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].FxLocationAsmb  = <-1.155106, 0, 0>  (float3)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].FxExhaustDirectionAsmb  = <-1, 0, 0>  (float3)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].SoundEvent  = KSA.RocketSoundEvent  (RocketSoundEvent)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].VolumetricExhaust  = KSA.VolumetricExhaustReference  (VolumetricExhaustReference)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].ExhaustLight  = True  (Boolean)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].Rocket  = KSA.Rocket  (Rocket)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].MaxExhaustDensity  = 0.0016401502  (Single)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].MaxExhaustTemperature  = 1031.1693  (Single)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].StatesIdx  = 38  (Int32)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].InstanceId  = 1327  (UInt64)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].TemplateId  = Nozzle  (String)
        part.SubParts[1].SubtreeComponents[RocketNozzle][0].ComputePerformance(RocketCoreConditions& coreConditions, Single ambientPressure)  ␦ NozzlePerformance
    [KSA.Rocket]  count=1
        part.SubParts[1].SubtreeComponents[Rocket][0].GetType()  = KSA.Rocket ␦ KSA.PartComponent`1[[KSA.Rocket, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].SubtreeComponents[Rocket][0].Core  = KSA.Combustor  (RocketCore)
        part.SubParts[1].SubtreeComponents[Rocket][0].Nozzles  = [1 items] RocketNozzle[]  (RocketNozzle[])
        part.SubParts[1].SubtreeComponents[Rocket][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].SubtreeComponents[Rocket][0].InstanceId  = 1328  (UInt64)
        part.SubParts[1].SubtreeComponents[Rocket][0].TemplateId  = Engine  (String)
    [KSA.FxTemperature]  count=1
        part.SubParts[1].SubtreeComponents[FxTemperature][0].GetType()  = KSA.FxTemperature ␦ KSA.PartComponentStateful`4[[KSA.FxTemperature, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.FxTemperatureState, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null],[KSA.EmptyStruct, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponent`1[[KSA.FxTemperature, KSA, Version=2026.2.32.3646, Culture=neutral, PublicKeyToken=null]] ␦ KSA.PartComponentBase
        part.SubParts[1].SubtreeComponents[FxTemperature][0].StatesIdx  = 6  (Int32)
        part.SubParts[1].SubtreeComponents[FxTemperature][0].Parent  = KSA.Part  (Part)
        part.SubParts[1].SubtreeComponents[FxTemperature][0].InstanceId  = 1329  (UInt64)
        part.SubParts[1].SubtreeComponents[FxTemperature][0].TemplateId  =   (String)
        part.SubParts[1].SubtreeComponents[FxTemperature][0].CreateStates(FxTemperatureState& state, EmptyStruct& fxState)  ␦ Void
  part.SubParts[1].SubParts = (none)
  part.SubParts[1].TreeChildren = (none)
part.SubParts[2] ───────────────────
  part.SubParts[2].GetType()          = KSA.Part
  part.SubParts[2].Id                 = CorePropulsionA_Subpart_EngineANozzleC1
  part.SubParts[2].DisplayName        = CorePropulsionA_Subpart_EngineANozzleC1
  part.SubParts[2].IsSubPart          = True
  part.SubParts[2].PartParent?.Id     = pixel_0_0_a
  part.SubParts[2].Components  = (empty - 0 types)
  part.SubParts[2].SubtreeComponents  = (empty - 0 types)
  part.SubParts[2].SubParts = (none)
  part.SubParts[2].TreeChildren = (none)
part.SubParts[3] ───────────────────
  part.SubParts[3].GetType()          = KSA.Part
  part.SubParts[3].Id                 = CorePropulsionA_Subpart_EngineAActuatorShaftCompact2
  part.SubParts[3].DisplayName        = CorePropulsionA_Subpart_EngineAActuatorShaftCompact2
  part.SubParts[3].IsSubPart          = True
  part.SubParts[3].PartParent?.Id     = pixel_0_0_a
  part.SubParts[3].Components  = (empty - 0 types)
  part.SubParts[3].SubtreeComponents  = (empty - 0 types)
  part.SubParts[3].SubParts = (none)
  part.SubParts[3].TreeChildren = (none)
part.SubParts[4] ───────────────────
  part.SubParts[4].GetType()          = KSA.Part
  part.SubParts[4].Id                 = CorePropulsionA_Subpart_EngineAActuatorShaftCompact1
  part.SubParts[4].DisplayName        = CorePropulsionA_Subpart_EngineAActuatorShaftCompact1
  part.SubParts[4].IsSubPart          = True
  part.SubParts[4].PartParent?.Id     = pixel_0_0_a
  part.SubParts[4].Components  = (empty - 0 types)
  part.SubParts[4].SubtreeComponents  = (empty - 0 types)
  part.SubParts[4].SubParts = (none)
  part.SubParts[4].TreeChildren = (none)
part.TreeChildren = (none)
═══════════ END DEBUG DUMP ════════════════════════════
```