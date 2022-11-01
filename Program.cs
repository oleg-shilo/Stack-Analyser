// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using static PdbHelper;
using static DbgHelper;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

// https://www.demo2s.com/csharp/csharp-localvariable-name.html
// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callsitehelpers?view=net-6.0#methods
// https://stackoverflow.com/questions/2405230/can-i-get-parameter-names-values-procedurally-from-the-currently-executing-funct

class Program
{
    static void Main()
    {
        method2();
    }

    static void method1()
    {
        var testVar1 = "asdASDAS"; Inspect();
    }

    static void method2()
    {
        var testVar2 = "ccccccccccccc";
        var testVar3 = "ccccccccccccc"; Inspect();

        for (int i = 0; i < 2; i++)
        {
            var testVar4 = "ccccccccccccc";
            var testVar5 = "ccccccccccccc";
        }
        method1();
    }
}

static class DbgHelper
{
    public static void Inspect(
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
    {
        StackTrace trace = new StackTrace();
        StackFrame frame = trace.GetFrame(1); // index `1` is for the caller frame

        MethodBase method = frame.GetMethod();
        MethodBody methodBody = method.GetMethodBody();

        ParameterInfo[] parameters = method.GetParameters();

        var pdb = new PdbHelper(Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".pdb"));

        var scope = pdb.ReadScopes().OrderBy(x => x.line).Where(x => x.line < sourceLineNumber).LastOrDefault();

        if (scope.token != 0)
        {
            Console.WriteLine("-------------------");

            // var signature = pdb.reader.GetStandaloneSignature(scope.method.LocalSignature); // local method (doesn't work yet)
            // Console.WriteLine(signature.DecodeLocalSignature(pdb.provider));

            Console.WriteLine(memberName.ToString()); // actual method name

            var variablesInfo = pdb.GetLocalVariableNamesForMethod(scope.token);

            if (methodBody != null)
            {
                foreach (var local in methodBody.LocalVariables)
                {
                    var name = variablesInfo.FirstOrDefault(x => x.Index == local.LocalIndex);
                    if (name != null)
                        Console.WriteLine($"    {local.LocalType} {name}");
                }
            }
        }
    }
}

class PdbHelper
{
    public MetadataReader reader;
    public MetadataReaderProvider provider;

    public PdbHelper(string pdbPath)
    {
        var stream = new StreamReader(pdbPath);
        provider = MetadataReaderProvider.FromPortablePdbStream(stream.BaseStream, MetadataStreamOptions.PrefetchMetadata, 0);
        reader = provider.GetMetadataReader();
    }

    public (int token, int line, string source, string localSignature, MethodDebugInformation method)[] ReadScopes()
    {
        var scopes = reader
            .MethodDebugInformation
            .Where(h => !h.IsNil)
            .Select(md => (reader.GetMethodDebugInformation(md), System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(md.ToDefinitionHandle())))
            .Where(m => !m.Item1.SequencePointsBlob.IsNil)
            .Select(m => m.Item1.GetSequencePoints()
                                .Select(sp =>
                                        {
                                            var document = reader.GetDocument(sp.Document);
                                            var name = reader.GetString(document.Name);
                                            // var scopeName = reader.GetString(m.Item1.LocalSignature); // ideally should be resolved here
                                            return (m.Item2, sp.StartLine, name, "", m.Item1);
                                        })
                                .OrderBy(x => x.StartLine)
                                .First());

        return scopes.ToArray();
    }

    public class LocalVariable
    {
        public LocalVariable(int index, string name, bool compilerGenerated)
        {
            Index = index;
            Name = name;
            CompilerGenerated = compilerGenerated;
        }

        public int Index { get; set; }
        public string Name { get; set; }
        public bool CompilerGenerated { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    void ProbeScopeForLocals(List<LocalVariable> variables, LocalScopeHandle localScopeHandle)
    {
        var localScope = reader.GetLocalScope(localScopeHandle);
        foreach (var localVariableHandle in localScope.GetLocalVariables())
        {
            var localVariable = reader.GetLocalVariable(localVariableHandle);
            var name = reader.GetString(localVariable.Name);

            bool compilerGenerated = (localVariable.Attributes & LocalVariableAttributes.DebuggerHidden) != 0;
            variables.Add(new LocalVariable(localVariable.Index, name, compilerGenerated));
        }
        var children = localScope.GetChildren();
        while (children.MoveNext())
        {
            ProbeScopeForLocals(variables, children.Current);
        }
    }

    public IEnumerable<LocalVariable> GetLocalVariableNamesForMethod(int methodToken)
    {
        var debugInformationHandle = MetadataTokens.MethodDefinitionHandle(methodToken).ToDebugInformationHandle();
        var localScopes = reader.GetLocalScopes(debugInformationHandle);
        var variables = new List<LocalVariable>();
        foreach (var localScopeHandle in localScopes)
        {
            ProbeScopeForLocals(variables, localScopeHandle);
        }
        // remove duplicates
        return variables.GroupBy(x => x.Index)
                        .Select(g => g.OrderBy(y => y.Index).First());
    }
}