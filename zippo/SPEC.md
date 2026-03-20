- imgui window with a vehicle combobox selector, default to no selection.  also allow de-selection (so have an empty entry? or some other imgui normal pattern for unset value)
- when a vehicle is selected, do a recursive search through its part list for any part which is Light part and create a combobox selector for light parts (default to empty and make it unsettable as well)

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

  you may need to debug data about the part/subpart tree to discover how
  to identify light parts because im unsure.  if thats the case put in 
  Console.WriteLine statements to debug the part props recursively with
  their key/value names etc and ask me to run the game and get the logs to provide back.
- when a vehicle and light part are selected, provide a on/off button to disable it or enable it if there is such a property (if not, defer to just using intensity setting)
- when a vehicle and light part are selected, show a drag float slider (which is full width for the imgui window) which goes from 0 to 1, and should default to the light parts emissive value when selected in the combobox.  when we change the value, update the emissive value on the light part.
- when a vehicle and light part are selected, show a combobox to change the light part color, use the KSAColor.Xkcd color constants: Marine, HotPink, RadioactiveGreen, BabyPurple.  when the value changes in the combobox, set it on the light part.