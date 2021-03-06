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
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SecurityFeatures)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("cwe", "owasp-a6", "sans-top25-porous", "security")]
    public class InsecureHashAlgorithm : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2070";
        internal const string Description = "SHA-1 and Message-Digest hash algorithms should not be used";
        internal const string MessageFormat = "Use a stronger encryption algorithm than {0}.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = false;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2070");

        private const string HashAlgorithmTypeName = "System.Security.Cryptography.HashAlgorithm";

        private static readonly Dictionary<string, string> InsecureHashAlgorithmTypeNames = new Dictionary<string, string>
        {
            { "System.Security.Cryptography.SHA1", "SHA1"},
            { "System.Security.Cryptography.MD5", "MD5"}
        };

        private static readonly string[] MethodNamesToReachHashAlgorithm =
        {
            "System.Security.Cryptography.CryptoConfig.CreateFromName",
            "System.Security.Cryptography.HashAlgorithm.Create",
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var objectCreation = (ObjectCreationExpressionSyntax) c.Node;
                    
                    var typeInfo = c.SemanticModel.GetTypeInfo(objectCreation);

                    if (typeInfo.ConvertedType == null || typeInfo.ConvertedType is IErrorTypeSymbol)
                    {
                        return;
                    }

                    var insecureArgorithmType = GetInsecureAlgorithmBase(typeInfo.ConvertedType);

                    if (insecureArgorithmType != null &&
                        InsecureHashAlgorithmTypeNames.ContainsKey(insecureArgorithmType.ToString()))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Type.GetLocation(), InsecureHashAlgorithmTypeNames[insecureArgorithmType.ToString()]));
                    }
                },
                SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax) c.Node;

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;

                    if (methodSymbol == null)
                    {
                        return;
                    }

                    var methodName = string.Format("{0}.{1}", methodSymbol.ContainingType.ToString(), methodSymbol.Name);
                    if (MethodNamesToReachHashAlgorithm.Contains(methodName) &&
                        IsArgumentMatchingAlgorithmName(invocation))
                    {
                        var algorithm = InsecureHashAlgorithmTypeNames.Values.First(algorithmName =>
                            ((LiteralExpressionSyntax) invocation.ArgumentList.Arguments.First().Expression).Token
                                .ValueText.StartsWith(algorithmName));
                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), algorithm));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool IsArgumentMatchingAlgorithmName(InvocationExpressionSyntax invocation)
        {
            return
                invocation.ArgumentList != null &&
                invocation.ArgumentList.Arguments.Count > 0 &&
                invocation.ArgumentList.Arguments.First().Expression.IsKind(SyntaxKind.StringLiteralExpression) &&
                InsecureHashAlgorithmTypeNames.Values.Any(algorithmName =>
                    ((LiteralExpressionSyntax) invocation.ArgumentList.Arguments.First().Expression).Token.ValueText
                        .StartsWith(algorithmName));
        }

        private static ITypeSymbol GetInsecureAlgorithmBase(ITypeSymbol type)
        {
            var typeChain = new List<ITypeSymbol>();

            var currentType = type;
            while (currentType != null)
            {
                typeChain.Add(currentType);
                currentType = currentType.BaseType;
            }
            
            var hashAlgorithmType = typeChain.FirstOrDefault(t => t.ToString() == HashAlgorithmTypeName);
            if (hashAlgorithmType == null)
            {
                return null;
            }

            var index = typeChain.IndexOf(hashAlgorithmType);

            return index == 0 ? null : typeChain[index - 1];
        }
    }
}
