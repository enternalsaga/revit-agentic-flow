using RevitMcpPlugin.Core;
using TUnit.Core;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace RevitMcpPlugin.Tests;

public class CommandAssemblyPathResolverTests
{
    [Test]
    public async Task ResolveRelativePath_WithCommandSetVersionAndDll_ReturnsDllPath()
    {
        var commandsDirectory = Path.Combine("C:", "Revit", "Addins", "revit-mcp", "Commands");
        var assemblyPath = Path.Combine("RevitMCPCommandSet", "{VERSION}", "RevitMCPCommandSet.dll");

        var resolved = CommandAssemblyPathResolver.Resolve(commandsDirectory, assemblyPath, "2025");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(commandsDirectory, "RevitMCPCommandSet", "2025", "RevitMCPCommandSet.dll"));
    }

    [Test]
    public async Task LooksLikeLegacyCommand_WithJObjectFromDifferentAssembly_StillMatchesByFullName()
    {
        var commandType = CreateLegacyCommandTypeWithForeignJObject();

        var matches = CommandReflectionUtils.LooksLikeLegacyCommand(commandType);

        await Assert.That(matches).IsTrue();
    }

    private static Type CreateLegacyCommandTypeWithForeignJObject()
    {
        var assemblyName = new AssemblyName("ForeignNewtonsoftCommandAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("ForeignNewtonsoftCommandModule");

        var jobjectBuilder = moduleBuilder.DefineType("Newtonsoft.Json.Linq.JObject", TypeAttributes.Public);
        var foreignJObjectType = jobjectBuilder.CreateType();

        var commandBuilder = moduleBuilder.DefineType("FakeLegacyCommand", TypeAttributes.Public);
        var commandNameProperty = commandBuilder.DefineProperty("CommandName", PropertyAttributes.None, typeof(string), Type.EmptyTypes);
        var getCommandName = commandBuilder.DefineMethod(
            "get_CommandName",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string),
            Type.EmptyTypes);
        var commandNameIl = getCommandName.GetILGenerator();
        commandNameIl.Emit(OpCodes.Ldstr, "fake_command");
        commandNameIl.Emit(OpCodes.Ret);
        commandNameProperty.SetGetMethod(getCommandName);

        var executeMethod = commandBuilder.DefineMethod(
            "Execute",
            MethodAttributes.Public,
            typeof(object),
            new[] { foreignJObjectType, typeof(string) });
        var executeIl = executeMethod.GetILGenerator();
        executeIl.Emit(OpCodes.Ldnull);
        executeIl.Emit(OpCodes.Ret);

        return commandBuilder.CreateType();
    }
}
