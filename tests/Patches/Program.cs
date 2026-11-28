using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2)
{
    Console.Error.WriteLine("Expected paths to GoodFriend.dll and sts2.dll.");
    return 2;
}
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
var reader = new ReaderParameters { AssemblyResolver = resolver };
using var game = ModuleDefinition.ReadModule(args[1], reader);
using var mod = ModuleDefinition.ReadModule(args[0], reader);
var errors = new List<string>();
int count = 0;

foreach (var patch in mod.Types)
{
    var annotations = patch.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToArray();
    if (annotations.Length == 0) continue;
    var arguments = annotations.SelectMany(a => a.ConstructorArguments).ToArray();
    var owner = arguments.FirstOrDefault(a => a.Type.FullName == "System.Type").Value as TypeReference;
    var targetName = arguments.FirstOrDefault(a => a.Type.FullName == "System.String").Value as string;
    var signature = arguments.FirstOrDefault(a => a.Type.FullName == "System.Type[]").Value as CustomAttributeArgument[];
    if (owner == null || targetName == null)
    {
        // This patch enumerates three property getters from their names in TargetMethods.
        if (patch.Name != "FriendPowerIcons")
        {
            errors.Add($"Missing or unverified target in {patch.Name}");
            continue;
        }
        var getterNames = patch.Methods.Single(m => m.Name == "TargetMethods").Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldstr).Select(i => "get_" + i.Operand).ToArray();
        if (getterNames.Length != 3) errors.Add("Expected three power icon getters");
        var power = game.GetType("MegaCrit.Sts2.Core.Models.PowerModel");
        foreach (var name in getterNames) Check(patch, power.Methods.SingleOrDefault(m => m.Name == name));
        continue;
    }

    var kind = arguments.SingleOrDefault(a => a.Type.FullName == "HarmonyLib.MethodType");
    var kindName = kind.Type?.Resolve().Fields.Single(f => f.HasConstant && Equals(f.Constant, kind.Value)).Name;
    if (kindName == "Getter") targetName = "get_" + targetName;
    var candidates = game.GetType(owner.FullName).Methods.Where(m => m.Name == targetName);
    if (signature != null)
    {
        var types = signature.Select(a => ((TypeReference)a.Value).FullName);
        candidates = candidates.Where(m => m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(types));
    }
    var target = candidates.SingleOrDefault();
    if (kindName == "Async" && target != null)
    {
        var attribute = target.CustomAttributes.Single(a => a.AttributeType.Name == "AsyncStateMachineAttribute");
        var state = ((TypeReference)attribute.ConstructorArguments[0].Value).Resolve();
        // The two-parameter Kill overload only delegates. The overload resolving victory retains the run state.
        if (!state.Fields.Any(f => f.FieldType.FullName == "MegaCrit.Sts2.Core.Runs.IRunState"))
            errors.Add($"{target.FullName} does not own the run's death resolution");
        target = state.Methods.SingleOrDefault(m => m.Name == "MoveNext");
    }
    Check(patch, target);
}

void Check(TypeDefinition patch, MethodDefinition? target)
{
    if (target == null)
    {
        errors.Add($"Undefined Harmony target for {patch.Name}");
        return;
    }
    count++;
    var sharedState = patch.Methods.FirstOrDefault(m => m.Name == "Prefix")?.Parameters
        .FirstOrDefault(p => p.Name == "__state")?.ParameterType as ByReferenceType;
    foreach (var hook in patch.Methods.Where(m => m.Name is "Prefix" or "Postfix"))
    foreach (var argument in hook.Parameters)
    {
        var expectedType = argument.Name switch
        {
            "__instance" => target.IsStatic ? null : target.DeclaringType,
            "__result" => target.ReturnType,
            "__state" => sharedState?.ElementType,
            _ => target.Parameters.SingleOrDefault(p => p.Name == argument.Name)?.ParameterType,
        };
        var declaredType = argument.ParameterType is ByReferenceType reference ? reference.ElementType : argument.ParameterType;
        if (expectedType?.FullName != declaredType.FullName)
            errors.Add($"{patch.Name}.{hook.Name} cannot bind {argument.Name} on {target.FullName}");
    }
}

foreach (var error in errors) Console.Error.WriteLine(error);
if (count == 0 || errors.Count != 0) return 1;
Console.WriteLine($"Verified {count} Harmony targets and their injected parameters against {game.Name} metadata.");
return 0;
