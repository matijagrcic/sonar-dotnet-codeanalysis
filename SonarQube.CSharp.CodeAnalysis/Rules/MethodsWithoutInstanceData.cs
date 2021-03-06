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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("performance")]
    public class MethodsWithoutInstanceData : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2325";
        internal const string Description = "Methods that don't access instance data should be static";
        internal const string MessageFormat = "Make \"{0}\" a \"static\" method.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2325");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartAction<SyntaxKind>(
                cbc =>
                {
                    var methodDeclaration = cbc.CodeBlock as MethodDeclarationSyntax;

                    if (methodDeclaration == null)
                    {
                        return;
                    }

                    var methodSymbol = cbc.SemanticModel.GetDeclaredSymbol(methodDeclaration);

                    if (methodSymbol == null ||
                        HasAllowedModifier(methodSymbol) ||
                        IsInterfaceImplementation(methodSymbol))
                    {
                        return;
                    }

                    var reportShouldBeStatic = true;
                    cbc.RegisterSyntaxNodeAction(c =>
                    {
                        var identifier = (IdentifierNameSyntax) c.Node;

                        var identifierSymbol = c.SemanticModel.GetSymbolInfo(identifier).Symbol;

                        if (identifierSymbol == null)
                        {
                            return;
                        }

                        if (PossibleMemberSymbolKinds.Contains(identifierSymbol.Kind) &&
                            !identifierSymbol.IsStatic)
                        {
                            reportShouldBeStatic = false;
                        }
                    },
                        SyntaxKind.IdentifierName);

                    cbc.RegisterSyntaxNodeAction(c =>
                    {
                        reportShouldBeStatic = false;
                    },
                        SyntaxKind.ThisExpression);


                    cbc.RegisterCodeBlockEndAction(c =>
                    {
                        if (reportShouldBeStatic)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), 
                                methodDeclaration.Identifier.Text));
                        }
                    });
                });
        }

        private static bool HasAllowedModifier(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsStatic ||
                   methodSymbol.IsVirtual ||
                   methodSymbol.IsOverride ||
                   methodSymbol.IsAbstract;
        }

        private static bool IsInterfaceImplementation(IMethodSymbol methodSymbol)
        {
            var containingType = methodSymbol.ContainingType;

            var interfaces = new Queue<INamedTypeSymbol>(containingType.Interfaces);
            var allInterfaces = new List<INamedTypeSymbol>();

            while (interfaces.Count > 0)
            {
                var @interface = interfaces.Dequeue();
                @interface.Interfaces.ToList().ForEach(interfaces.Enqueue);
                allInterfaces.Add(@interface);
            }

            return allInterfaces
                .SelectMany(interf => interf.GetMembers().OfType<IMethodSymbol>())
                .Any(interfaceMember => methodSymbol == containingType.FindImplementationForInterfaceMember(interfaceMember));
        }

        private static readonly SymbolKind[] PossibleMemberSymbolKinds =
        {
            SymbolKind.Method,
            SymbolKind.Field,
            SymbolKind.Property,
            SymbolKind.Event
        };
    }
}
