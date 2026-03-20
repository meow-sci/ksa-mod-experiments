- imgui window with a vehicle selector, default to no selection.  also allow de-selection (so have an empty entry? or some other imgui normal pattern for unset value)
- when a vehicle is selected, do a recursive search through its part list for any part which is Light part

  vehicles and parts their subparts are defined in XML before the game loads them into memory as objects.  here's an example of a light part we would want to identify from the xml perspective

  ```xml
  <SubPart Id="CoreInternalA_Subpart_Assets">
    <PartModel Id="CoreInternalA_Subpart_Assets_Model">
        <Mesh Id="RaytracingIVA_Assets"/>
        <Material Id="CorePropsA_Material"/>
        <RayTracing>true</RayTracing>
    </PartModel>

    <Light>
        <Type>Point</Type>
        <Transform>
            <Position X="-0.275" Z="-0.80"/>
        </Transform>
        <Range Value="1.5"/>
        <Intensity Value="0.05"/>
        <Color R="1.0" G="0.9" B="0.7"/>
    </Light>
  </SubPart>
  ```