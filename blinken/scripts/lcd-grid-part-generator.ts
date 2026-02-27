const INITIAL_CONFIG: InitialConfig = {
  scale: 0.01,
  position: { x: -4.0, y: 2, z: 0 },
  rotation: { x: 0, y: -1.5708, z: 0 },
  reverseRotation: { x: 0, y: 1.5708, z: 0 },
  stage: 0,
  rows: 53,
  cols: 18,
  initialLocalInstanceId: 1000,
  positionGap: 4.0
};

interface InitialConfig {
  scale: number;
  position: XYZ;
  rotation: XYZ;
  reverseRotation: XYZ;
  stage: number;
  rows: number;
  cols: number;
  initialLocalInstanceId: number;
  positionGap: number;
}


interface LocalInstanceId {
  current: number;
}

interface XYZ {
  x?: number;
  y?: number;
  z?: number;
}

interface PartConfig {
  idPrefix: string;
  scale: number;
  position: XYZ;
  rotation: XYZ;
  reverseRotation: XYZ;
  localInstanceId: LocalInstanceId;
  stage: number;
  x: number;
  y: number;
}


function enginePartTemplate(config: PartConfig): string {

  return `
            <PartRef Id="${config.idPrefix}_a" InstanceOf="CorePropulsionA_Prefab_EngineA4" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}">
              <Transform>
                <Position X="${config.position.x ?? "0"}" Y="${config.position.y ?? "0"}" Z="${config.position.z ?? "0"}" />
                <Rotation X="${config.rotation.x ?? "0"}" Y="${config.rotation.y ?? "0"}" Z="${config.rotation.z ?? "0"}" />
                <Scale X="${config.scale ?? "1"}" Y="${config.scale ?? "1"}" Z="${config.scale ?? "1"}" />
              </Transform>
              <PartConnectorRef Index="0" ConnectedLocalInstanceId="${config.localInstanceId.current++}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineA1WBaseCompact" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineACompactVacAssembly" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineANozzleC" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineAActuatorShaftCompact" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineAActuatorShaftCompact" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <EngineController InstanceOf="LR91-AJ-3" LocalInstanceId="${config.localInstanceId.current++}" ActiveInStage="true" />
            </PartRef>
            <PartRef Id="${config.idPrefix}_b" InstanceOf="CorePropulsionA_Prefab_EngineA4" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}">
              <Transform>
                <Position X="${config.position.x ?? "0"}" Y="${config.position.y ?? "0"}" Z="${config.position.z ?? "0"}" />
                <Rotation X="${config.reverseRotation.x ?? "0"}" Y="${config.reverseRotation.y ?? "0"}" Z="${config.reverseRotation.z ?? "0"}" />
                <Scale X="${config.scale ?? "1"}" Y="${config.scale ?? "1"}" Z="${config.scale ?? "1"}" />
              </Transform>
              <PartConnectorRef Index="0" ConnectedLocalInstanceId="${config.localInstanceId.current++}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineA1WBaseCompact" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineACompactVacAssembly" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineANozzleC" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineAActuatorShaftCompact" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <SubPartRef InstanceOf="CorePropulsionA_Subpart_EngineAActuatorShaftCompact" LocalInstanceId="${config.localInstanceId.current++}" Stage="${config.stage}" />
              <EngineController InstanceOf="LR91-AJ-3" LocalInstanceId="${config.localInstanceId.current++}" ActiveInStage="true" />
            </PartRef>
`;

}

function generateEngineGrid(config: InitialConfig): string {

  let parts = "";

  const localInstanceId: LocalInstanceId = { current: config.initialLocalInstanceId };

  for (let row = 0; row < config.rows; row++) {
    for (let col = 0; col < config.cols; col++) {

      let idPrefix = `pixel_${row}_${col}`;

      const partConfig: PartConfig = {
        idPrefix: idPrefix,
        scale: config.scale,
        position: {
          x: col * config.positionGap,
          y: row * config.positionGap,
          z: 0
        },
        rotation: config.rotation,
        reverseRotation: config.reverseRotation,
        localInstanceId,
        stage: config.stage,
        x: col,
        y: row
      };

      parts += enginePartTemplate(partConfig);

    }
  }

  return parts;

}

console.log(generateEngineGrid(INITIAL_CONFIG));
