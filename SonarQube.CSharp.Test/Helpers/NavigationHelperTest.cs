﻿/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.Runner;

namespace SonarQube.CSharp.Test.Helpers
{
    [TestClass]
    public class NavigationHelperTest
    {
        private const string Source = @"
namespace Test
{
    class TestClass
    {        
        public void DoSomething(){}
        public void IfMethod()
        {
            if (true)
                DoSomething();
            else if (true)
                DoSomething();
            else
                DoSomething();  
        }

        public void SwitchMethod()
        {
            var i = 5;
            switch(i)
            {
                case 3:
                    DoSomething();
                    break;
                case 5:
                    DoSomething();
                    break;
                default:
                    DoSomething();
                    break;
            }  
        }
    }
}";
        private Solution solution;
        private Compilation compilation;
        private SyntaxTree syntaxTree;
        private MethodDeclarationSyntax ifMethod;
        private MethodDeclarationSyntax switchMethod;

        [TestInitialize]
        public void TestSetup()
        {
            solution = CompilationHelper.GetSolutionFromText(Source);

            compilation = solution.Projects.First().GetCompilationAsync().Result;
            syntaxTree = compilation.SyntaxTrees.First();

            ifMethod = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ValueText == "IfMethod");
            switchMethod = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ValueText == "SwitchMethod");
        }

        [TestMethod]
        public void GetPrecedingIfsInConditionChain()
        {
            var ifStatement1 = ifMethod.DescendantNodes().OfType<IfStatementSyntax>().First();
            ifStatement1.GetPrecedingIfsInConditionChain().Should().HaveCount(0);

            var ifStatement2 = ifMethod.DescendantNodes().OfType<IfStatementSyntax>().Last();
            var preceding = ifStatement2.GetPrecedingIfsInConditionChain();
            preceding.Should().HaveCount(1);

            ifStatement1.ShouldBeEquivalentTo(preceding[0]);
        }

        [TestMethod]
        public void GetPrecedingStatementsInConditionChain()
        {
            var ifStatement1 = ifMethod.DescendantNodes().OfType<IfStatementSyntax>().First();
            ifStatement1.GetPrecedingStatementsInConditionChain().Should().HaveCount(0);

            var ifStatement2 = ifMethod.DescendantNodes().OfType<IfStatementSyntax>().Last();
            var preceding = ifStatement2.GetPrecedingStatementsInConditionChain().ToList();
            preceding.Should().HaveCount(1);

            ifStatement1.Statement.ShouldBeEquivalentTo(preceding[0]);
        }

        [TestMethod]
        public void GetPrecedingConditionsInConditionChain()
        {
            var ifStatement1 = ifMethod.DescendantNodes().OfType<IfStatementSyntax>().First();
            ifStatement1.GetPrecedingConditionsInConditionChain().Should().HaveCount(0);

            var ifStatement2 = ifMethod.DescendantNodes().OfType<IfStatementSyntax>().Last();
            var preceding = ifStatement2.GetPrecedingConditionsInConditionChain().ToList();
            preceding.Should().HaveCount(1);

            ifStatement1.Condition.ShouldBeEquivalentTo(preceding[0]);
        }

        [TestMethod]
        public void GetPrecedingSections()
        {
            var sections = switchMethod.DescendantNodes().OfType<SwitchSectionSyntax>().ToList();

            sections.Last().GetPrecedingSections().Should().HaveCount(2);
            sections.First().GetPrecedingSections().Should().HaveCount(0);
            sections.Last().GetPrecedingSections().First().ShouldBeEquivalentTo(sections.First());
        }


        [TestMethod]
        public void GetPrecedingStatement()
        {
            var statements = switchMethod.Body.Statements.ToList();

            statements[1].GetPrecedingStatement().ShouldBeEquivalentTo(statements[0]);

            statements[0].GetPrecedingStatement().ShouldBeEquivalentTo(null);
        }

        [TestMethod]
        public void GetSucceedingStatement()
        {
            var statements = switchMethod.Body.Statements.ToList();

            statements[0].GetSucceedingStatement().ShouldBeEquivalentTo(statements[1]);

            statements[1].GetSucceedingStatement().ShouldBeEquivalentTo(null);
        }
    }
}
