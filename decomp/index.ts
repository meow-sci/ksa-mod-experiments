import { $ } from "bun";
import {join} from "node:path";

const outDir = join(__dirname, "ksa");


await $`dotnet tool run ilspycmd -o ${outDir} -p -r 'C:\Program Files\Kitten Space Agency'  'C:\Program Files\Kitten Space Agency\Brutal.ImGui.Abstractions.dll'`;
await $`dotnet tool run ilspycmd -o ${outDir} -p -r 'C:\Program Files\Kitten Space Agency'  'C:\Program Files\Kitten Space Agency\Brutal.ImGui.Extensions.dll'`;
await $`dotnet tool run ilspycmd -o ${outDir} -p -r 'C:\Program Files\Kitten Space Agency'  'C:\Program Files\Kitten Space Agency\Brutal.ImGui.dll'`;
await $`dotnet tool run ilspycmd -o ${outDir} -p -r 'C:\Program Files\Kitten Space Agency'  'C:\Program Files\Kitten Space Agency\KSA.dll'`;
