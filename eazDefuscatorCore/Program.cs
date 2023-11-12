using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System.IO;

namespace eazDefuscatorCore
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("missing file name.");
                Console.Read();
                return;
            }

            MethodDef DecryptMethod = null;
            MethodDef ActualDecryptMethod = null;
            Assembly assembly = null;
            try
            {
                assembly = Assembly.LoadFrom(args[0]);
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("Not a NET module.");
                Console.Read();
                return;
            }
            catch (FileLoadException)
            {
                Console.WriteLine("Cant load file.");
                Console.Read();
                return;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("File not found.");
                Console.Read();
                return;
            }
            AssemblyName[] dllRef = assembly.GetReferencedAssemblies();
            ModuleContext modCtx = ModuleDef.CreateModuleContext();
            ModuleDefMD module = ModuleDefMD.Load(args[0], modCtx);


            var classi = module.GetTypes().ToArray();
            List<MethodDef> methodList = new List<MethodDef>();

            //Get all methods
            foreach (var classe in classi)
            {
                for (int i = 0; i < classe.Methods.Count; i++)
                {
                    methodList.Add(classe.Methods[i]);
                }
            }
            //And Search string decryptor method
            foreach (var method in methodList)
            {
                if (method.Parameters.Count == 1 && method.ReturnType.FullName == "System.String" && method.Parameters[0].Type.FullName == "System.Int32" && method.HasBody == true && method.IsStatic == true)
                {
                    DecryptMethod = method;
                    foreach(var instruction in DecryptMethod.Body.Instructions)
                    {
                        if(instruction.Operand is MethodDef)
                        {
                            MethodDef temp = (MethodDef)instruction.Operand;
                            if(temp.Parameters.Count == 2 && temp.ReturnType.FullName == "System.String" && temp.Parameters[0].Type.FullName == "System.Int32" && temp.Parameters[1].Type.FullName == "System.Boolean" )
                            {
                                ActualDecryptMethod = temp;
                            }
                        }
                    }
                    break;
                }
            }

            if(DecryptMethod == null || ActualDecryptMethod == null)
            {
                Console.WriteLine("Cant find decryption method. Not an EazFuscator or something gone wrong.");
                Console.Read();
                return;
            }

            //Init string decrypt class
            assembly.GetModules()[0].ResolveMethod(DecryptMethod.DeclaringType.Methods[0].MDToken.ToInt32()).Invoke(null, new object[] { });
            foreach (MethodDef method in methodList)
            {
                if(method.HasBody == false) { continue; }
                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    var instruction = method.Body.Instructions[i];
                    if (instruction.OpCode.Name == "call" && instruction.Operand == DecryptMethod )
                    {
                        Int32 value = (Int32)method.Body.Instructions[i - 1].Operand;
                        String decryptedString = (String)assembly.GetModules()[0].ResolveMethod(ActualDecryptMethod.MDToken.ToInt32()).Invoke(null, new object[] { value, false });
                        //Patch
                        method.Body.Instructions[i - 1].OpCode = OpCodes.Ldstr;
                        method.Body.Instructions[i - 1].Operand = decryptedString;
                        instruction.OpCode = OpCodes.Nop;
                        File.AppendAllText(args[0] + "_strings.txt", decryptedString + "\n");
                    }
                }
            }
            Console.WriteLine("String decrypted");

            ModuleWriterOptions opt = new ModuleWriterOptions(module);
            opt.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
            module.Write(args[0] + "_decrypted.dll", opt);
            Console.WriteLine("Patch completed");
            Console.ReadLine();
        }
    }
}
