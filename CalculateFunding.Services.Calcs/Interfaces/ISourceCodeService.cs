﻿using System.Collections.Generic;
using System.Threading.Tasks;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Code;

namespace CalculateFunding.Services.Calcs.Interfaces
{
    public interface ISourceCodeService
    {
        Task SaveAssembly(BuildProject buildProject);

        Task<byte[]> GetAssembly(BuildProject buildProject);

        Build Compile(BuildProject buildProject, IEnumerable<Calculation> calculations, CompilerOptions compilerOptions = null);

        Task<IEnumerable<TypeInformation>> GetTypeInformation(BuildProject buildProject);

        IDictionary<string, string> GetCalculationFunctions(IEnumerable<SourceFile> sourceFiles);

        Task SaveSourceFiles(IEnumerable<SourceFile> sourceFiles, string specificationId, SourceCodeType sourceCodeType);

        Task DeleteAssembly(string specificationId);

        IEnumerable<SourceFile> GenerateSourceFiles(BuildProject buildProject, IEnumerable<Calculation> calculations, CompilerOptions compilerOptions);
    }
}
