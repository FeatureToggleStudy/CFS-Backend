using System;
using System.Collections.Generic;
using System.Linq;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Services.CodeGeneration.VisualBasic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CalculateFunding.Services.CodeMetadataGenerator.Vb.UnitTests
{
    [TestClass]
    public class CalculationTypeGeneratorTests
    {
        [TestMethod]
        [DataRow("Range 3", "Range3")]
        [DataRow("Range < 3", "RangeLessThan3")]
        [DataRow("Range > 3", "RangeGreaterThan3")]
        [DataRow("Range � 3", "RangePound3")]
        [DataRow("Range % 3", "RangePercent3")]
        [DataRow("Range = 3", "RangeEquals3")]
        [DataRow("Range + 3", "RangePlus3")]
        [DataRow("Nullable(Of Decimal)", "Nullable(Of Decimal)")]
        [DataRow("Nullable(Of Integer)", "Nullable(Of Integer)")]
        public void GenerateIdentifier_IdentifiersSubstituted(string input, string expected)
        {
            // Act
            string result = CalculationTypeGenerator.GenerateIdentifier(input);

            // Assert
            result
                .Should()
                .Be(expected);
        }

        [TestMethod]
        [DataRow("return calc1()", "return calc1()")]
        [DataRow("return AvgTest()", "return AvgTest()")]
        [DataRow("return SumTest()", "return SumTest()")]
        [DataRow("return TestMinTest()", "return TestMinTest()")]
        [DataRow("return Avg(calc1)", "return Avg(\"calc1\")")]
        [DataRow("return Max(calc1)", "return Max(\"calc1\")")]
        [DataRow("return Min(calc1)", "return Min(\"calc1\")")]
        [DataRow("return Sum(calc1)", "return Sum(\"calc1\")")]
        [DataRow("return    Max(calc1)", "return    Max(\"calc1\")")]
        [DataRow("return TestSum() + Max(calc1)", "return TestSum() + Max(\"calc1\")")]
        [DataRow("return    Max(calc1) + Sum(calc1)", "return    Max(\"calc1\") + Sum(\"calc1\")")]
        [DataRow("return    Max( calc1     ) + Sum(    calc1    )", "return    Max(\"calc1\") + Sum(\"calc1\")")]
        [DataRow("If(InpAdjPupilNumberGuaranteed= \"YES\" and InputAdjNOR > NOREstRtoY11) then", "If(InpAdjPupilNumberGuaranteed= \"YES\" and InputAdjNOR > NOREstRtoY11) then")]
        [DataRow("FundingRate1 = -1 * (Math.Min(ThresholdZ, FundingRateZ) / FundingRateZ) * Condition1 + Sum(test1)", "FundingRate1 = -1 * (Math.Min(ThresholdZ, FundingRateZ) / FundingRateZ) * Condition1 + Sum(\"test1\")")]
        public void QuoteAggregateFunctionCalls_QuotesAsExpected(string input, string expected)
        {
            //Act
            string result = CalculationTypeGenerator.QuoteAggregateFunctionCalls(input);

            //Assert
            result
                .Should()
                .Be(expected);
        }

        [TestMethod]
        public void GenerateCalcs_GivenCalculationsAndCompilerOptionsStrictOn_ThenOptionStrictGenerated()
        {
            // Arrange
            List<Calculation> calculations = new List<Calculation>();

            CompilerOptions compilerOptions = new CompilerOptions
            {

                OptionStrictEnabled = true
            };

            CalculationTypeGenerator calculationTypeGenerator = new CalculationTypeGenerator(compilerOptions);

            // Act
            IEnumerable<SourceFile> results = calculationTypeGenerator.GenerateCalcs(calculations);

            // Assert
            results.Should().HaveCount(1);
            results.First().SourceCode.Should().StartWith("Option Strict On");
        }

        [TestMethod]
        public void GenerateCalcs_GivenCalculationsAndCompilerOptionsOff_ThenOptionsGenerated()
        {
            // Arrange
            List<Calculation> calculations = new List<Calculation>();

            CompilerOptions compilerOptions = new CompilerOptions
            {
                OptionStrictEnabled = false
            };

            CalculationTypeGenerator calculationTypeGenerator = new CalculationTypeGenerator(compilerOptions);

            // Act
            IEnumerable<SourceFile> results = calculationTypeGenerator.GenerateCalcs(calculations);

            // Assert
            results.Should().HaveCount(1);
            results.First().SourceCode.Should().StartWith("Option Strict Off");
        }

        [TestMethod]
        [DataRow(false, "Inherits BaseCalculation")]
        [DataRow(true, "Inherits LegacyBaseCalculation")]
        public void GenerateCalcs_GivenCalculationsAndCompilerOptionsUseLegacyCodeIsSet_TheEnsuresCorrectInheritanceStatement(bool useLegacyCode, string expectedInheritsStatement)
        {
            // Arrange
            CompilerOptions compilerOptions = new CompilerOptions
            {
                UseLegacyCode = useLegacyCode,
            };

            CalculationTypeGenerator calculationTypeGenerator = new CalculationTypeGenerator(compilerOptions);

            // Act
            IEnumerable<SourceFile> results = calculationTypeGenerator.GenerateCalcs(new List<Calculation>());

            // Assert
            results.Should().HaveCount(1);

            results.First().SourceCode.Should().Contain(expectedInheritsStatement);
        }

        [TestMethod]
        public void GenerateCalcs_InvalidSourceCodeNormaliseWhitespaceFails_ReturnsError()
        {
            CompilerOptions compilerOptions = new CompilerOptions();
            CalculationTypeGenerator calculationTypeGenerator = new CalculationTypeGenerator(compilerOptions);

            string badCode = @"Dim Filter as Decimal
Dim APTPhase as String
Dim CensusPhase as String
Dim Result as Decimal

Filter = FILTERSBSAcademiesFilter()
APTPhase = Datasets.APTInputsAndAdjustments.Phase()
CensusPhase = Datasets.CensusPupilCharacteristics.Phase()

If Filter = 0 then

Return Exclude()

Else

If string.isnullorempty(APTPhase) then Result = 0

Else If CensusPhase = ""PRIMARY"" then Result = 1

Else If CensusPhase = ""MIDDLE-DEEMED PRIMARY"" then Result = 2

Else If CensusPhase = ""SECONDARY"" then Result = 3

Else If CensusPhase = ""MIDDLE-DEEMED SECONDARY"" then Result = 4

End If
End If
End If

Return Result + 0";

            IEnumerable<Calculation> calculations = new[] { new Calculation { Current = new CalculationVersion { SourceCode = badCode }, SourceCodeName = "Broken" } };

            Action generate = () => calculationTypeGenerator.GenerateCalcs(calculations).ToList();

            generate
                .Should()
                .Throw<Exception>()
                .And.Message
                .Should()
                .StartWith("Error compiling source code. Please check your code's structure is valid. ");
        }

        [TestMethod]
        public void GenerateCalcs_MissingSourceCodeName_ReturnsError()
        {
            string id = "42";

            CompilerOptions compilerOptions = new CompilerOptions();
            CalculationTypeGenerator calculationTypeGenerator = new CalculationTypeGenerator(compilerOptions);

            IEnumerable<Calculation> calculations = new[] { new Calculation { Current = new CalculationVersion { SourceCode = "Return 1" }, Id = id } };

            Action generate = () => calculationTypeGenerator.GenerateCalcs(calculations).ToList();

            generate
                .Should()
                .Throw<Exception>()
                .And.Message
                .Should()
                .Be($"Calculation source code name is not populated for calc {id}");
        }
    }
}
