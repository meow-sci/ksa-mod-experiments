import { $ } from "bun";
import { join } from "node:path";

const outDir = join(__dirname, "ksa");


const DLLS = [
  "Brutal.Concurrency.dll",
  "Brutal.Core.Collections.dll",
  "Brutal.Core.Common.dll",
  "Brutal.Core.Logging.dll",
  "Brutal.Core.Maths.dll",
  "Brutal.Core.Memory.dll",
  "Brutal.Core.Numerics.dll",
  "Brutal.Core.Package.dll",
  "Brutal.Core.Strings.dll",
  "Brutal.Fmod.dll",
  "Brutal.Glfw.dll",
  "Brutal.Gli.dll",
  "Brutal.Gli.Texture.dll",
  "Brutal.Gltf.dll",
  "Brutal.ImGui.Abstractions.dll",
  "Brutal.ImGui.dll",
  "Brutal.ImGui.Extensions.dll",
  "Brutal.ImPlot.dll",
  "Brutal.Ktx.dll",
  "Brutal.Ktx.Texture.dll",
  "Brutal.Monitor.Common.dll",
  "Brutal.Monitor.Host.dll",
  "Brutal.RakNet.dll",
  "Brutal.Render.Common.dll",
  "Brutal.Render.Mesh.dll",
  "Brutal.ShaderCompiler.dll",
  "Brutal.Stb.dll",
  "Brutal.Stb.Texture.dll",
  "Brutal.Texture.Abstractions.dll",
  "Brutal.Texture.dll",
  "Brutal.Vulkan.Abstractions.dll",
  "Brutal.Vulkan.dll",
  "KSA.dll",
  "Planet.Core.dll",
  "Planet.Render.Core.dll",
];

for (const dll of DLLS) {
  console.log(`Decompiling ${dll}...`);
  const dllPath = join("C:", "Program Files", "Kitten Space Agency", dll);
  await $`dotnet tool run ilspycmd -o ${outDir} -p -r 'C:\Program Files\Kitten Space Agency' ${dllPath}`;
}
