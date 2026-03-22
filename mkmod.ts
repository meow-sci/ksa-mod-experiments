import { parseArgs } from "node:util";
import { access, constants, mkdir, readdir } from "node:fs/promises";
import { join } from "node:path";

const PLACEHOLDER_KEBAB = "fixme-mod-name";
const PLACEHOLDER_PASCAL = "FixmeModName";
const TEMPLATE_DIRS = [PLACEHOLDER_KEBAB, `${PLACEHOLDER_KEBAB}.lib`];
const SKIP_DIRS = new Set(["bin", "obj"]);

function usage(): string {
	return [
		"Usage: bun run mkmod.ts <mod-name-kebab> <ModNamePascal>",
		"",
		"Example:",
		"  bun run mkmod.ts orbital-cam OrbitalCam",
	].join("\n");
}

function replacePlaceholders(value: string, kebabName: string, pascalName: string): string {
	return value
		.replaceAll(PLACEHOLDER_KEBAB, kebabName)
		.replaceAll(PLACEHOLDER_PASCAL, pascalName);
}

function validateNames(kebabName: string, pascalName: string): void {
	const kebabPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
	const pascalPattern = /^[A-Z][A-Za-z0-9]*$/;

	if (!kebabPattern.test(kebabName)) {
		throw new Error(`Invalid kebab-case mod name: ${kebabName}`);
	}

	if (!pascalPattern.test(pascalName)) {
		throw new Error(`Invalid PascalCase mod name: ${pascalName}`);
	}
}

async function pathExists(path: string): Promise<boolean> {
	try {
		await access(path, constants.F_OK);
		return true;
	} catch {
		return false;
	}
}

async function copyTemplateTree(
	sourceDir: string,
	targetDir: string,
	kebabName: string,
	pascalName: string,
): Promise<void> {
	await mkdir(targetDir, { recursive: true });
	const entries = await readdir(sourceDir, { withFileTypes: true });

	for (const entry of entries) {
		if (entry.isDirectory() && SKIP_DIRS.has(entry.name)) {
			continue;
		}

		const sourcePath = join(sourceDir, entry.name);
		const rewrittenEntryName = replacePlaceholders(entry.name, kebabName, pascalName);
		const targetPath = join(targetDir, rewrittenEntryName);

		if (entry.isDirectory()) {
			await copyTemplateTree(sourcePath, targetPath, kebabName, pascalName);
			continue;
		}

		if (entry.isFile()) {
			const sourceContent = await Bun.file(sourcePath).text();
			const rewrittenContent = replacePlaceholders(sourceContent, kebabName, pascalName);
			await Bun.write(targetPath, rewrittenContent);
			continue;
		}

		console.log(`Skipping unsupported entry type: ${sourcePath}`);
	}
}

async function main(): Promise<void> {
	const { positionals } = parseArgs({
		args: Bun.argv.slice(2),
		options: {},
		allowPositionals: true,
		strict: true,
	});

	if (positionals.length !== 2) {
		throw new Error(usage());
	}

	const [kebabName, pascalName] = positionals;

	if (!kebabName || !pascalName) {
		throw new Error(usage());
	}

	validateNames(kebabName, pascalName);

	for (const templateDirName of TEMPLATE_DIRS) {
		const sourceDir = join(process.cwd(), templateDirName);
		const targetDirName = replacePlaceholders(templateDirName, kebabName, pascalName);
		const targetDir = join(process.cwd(), targetDirName);

		if (!(await pathExists(sourceDir))) {
			throw new Error(`Template directory not found: ${sourceDir}`);
		}

		if (await pathExists(targetDir)) {
			throw new Error(`Target directory already exists: ${targetDir}`);
		}

		await copyTemplateTree(sourceDir, targetDir, kebabName, pascalName);
		console.log(`Created ${targetDirName}`);
	}

	console.log(`Scaffolded mod '${kebabName}' (${pascalName}) from fixme templates.`);
}

await main();
