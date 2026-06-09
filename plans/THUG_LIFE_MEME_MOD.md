# overview

i added a thug-life and thug-life.lib pair of csproj's to the project already for a thug-life mod

use the ksa, mod-impl, imgui, imgui-design skills for details 

what i want this mod to do is apply the "thug life" meme to in-game 3d space.  this meme is a simple PNG of some black sunglasses with white dots representing a sunflare/glare on the lense, the style is blocky (which is intentional).

this png can be found in file `thug-life.lib\img\thuglife.png`

use the quad rendering information from the ksa skill, this is known to work in other mods (NOT in this project).

i want this to be a submod with a set of UI widgets that will let me pick the vehicle, part and subpart to anchor the quad to, and let me
set the rotation / position offsets for the quad relative to subpart.

this should be similar to how garrys-torch lets us weld vehicles together, but instead we applying a 2d quad sunglasses

follow a similar UI pattern as garrys-torch but tailored to the behavior of this new thug-life mod

you can either embed the thuglife.png into the program and read it (i think this is possible with csharp/dotnet) and use it as a texture, or just generate a texture using code given how simple this PNG is; if you do this, it should match the same design as the PNG
