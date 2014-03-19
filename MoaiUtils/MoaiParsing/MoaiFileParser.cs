﻿using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MoaiUtils.MoaiParsing.CodeGraph;

namespace MoaiUtils.MoaiParsing {
    public class MoaiFileParser {

        public static void ParseMoaiCodeFile(FileInfo codeFile, FilePosition filePosition, MoaiTypeCollection types, WarningList warnings) {
            string code = File.ReadAllText(codeFile.FullName);
            ParseMoaiCodeFile(code, filePosition, types, warnings);
        }

        public static void ParseMoaiCodeFile(string code, FilePosition filePosition, MoaiTypeCollection types, WarningList warnings) {
            ParseDocumentationBlocks(code, filePosition, types, warnings);
            WarnForUndocumentedLuaMethods(code, filePosition, warnings);
        }

        private static readonly Regex documentationRegex = new Regex(@"
            /\*\*\s*
                # Documentation
                (?<annotation>(?<!\S)@[\s\S]*?)+
            \*/\s*
            (
                # Class definition
                (class|struct)\s+
                (?<className>[A-Za-z0-9_]+)\s*
                (
                    :\s*
                    (
                        ((public|protected|private|virtual)\s*)+
                        (?<baseClassName>[A-Za-z0-9_:<,\s>]+?)\s*
                        ,?\s*
                    )+
                    {
                )?
                |
                # Method definition
                int\s+(?<className>[A-Za-z0-9_]+)\s*::\s*(?<methodName>[A-Za-z0-9_]+)\s*\([^)]*\)\s*
                (
                    \{(?<methodBody>[\s\S]*?)(^\}|//-----------------------------)
                )?
            )", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static void ParseDocumentationBlocks(string code, FilePosition filePosition, MoaiTypeCollection types, WarningList warnings) {
            // Find all documentation blocks
            var matches = documentationRegex.Matches(code);

            // Parse documentation blocks
            foreach (Match match in matches) {
                // A documentation block may be attached to a type or to a method.
                string typeName = match.Groups["className"].Value;
                FilePosition documentationPosition = match.Groups["methodName"].Success
                    ? new MethodPosition(filePosition, typeName, match.Groups["methodName"].Value)
                    : new TypePosition(filePosition, typeName);

                // Parse annotations, filtering out unknown ones
                Annotation[] annotations = match.Groups["annotation"].Captures
                    .Cast<Capture>()
                    .Select(capture => Annotation.Create(capture.Value, documentationPosition, warnings))
                    .ToArray();
                foreach (var unknownAnnotation in annotations.OfType<UnknownAnnotation>()) {
                    warnings.Add(documentationPosition, WarningType.UnexpectedAnnotation,
                        "Unknown annotation command '{0}'.", unknownAnnotation.Command);
                }
                annotations = annotations
                    .Where(annotation => !(annotation is UnknownAnnotation))
                    .ToArray();

                // Parse annotation block
                MoaiType type = types.GetOrCreate(typeName, documentationPosition);
                if (documentationPosition is MethodPosition) {
                    // The documentation was attached to a method definition

                    // Get method body
                    string methodBody = match.Groups["methodBody"].Value;

                    MoaiMethodParser.ParseMethodDocumentation(type, annotations, methodBody, (MethodPosition) documentationPosition, types, warnings);
                } else {
                    // The documentation was attached to a type definition

                    // Get base type names, ignoring all template classes
                    MoaiType[] baseTypes = match.Groups["baseClassName"].Captures
                        .Cast<Capture>()
                        .Where(capture => !capture.Value.Contains("<"))
                        .Select(capture => types.GetOrCreate(capture.Value, null))
                        .ToArray();

                    var typePosition = (TypePosition) documentationPosition;
                    type.TypePosition = typePosition;
                    MoaiTypeParser.ParseTypeDocumentation(type, annotations, baseTypes, typePosition, warnings);
                }
            }
        }

        private static readonly Regex undocumentedLuaMethodRegex = new Regex(@"
            # No documentation preceding
            (?<!\*/\s*)
            # Lua method definition
            int\s+(?<className>[A-Za-z0-9_]+)\s*::\s*(?<methodName>_[A-Za-z0-9_]+)\s*\(
            ", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static void WarnForUndocumentedLuaMethods(string code, FilePosition filePosition, WarningList warnings) {
            var matches = undocumentedLuaMethodRegex.Matches(code);
            foreach (Match match in matches) {
                string typeName = match.Groups["className"].Value;
                string methodName = match.Groups["methodName"].Value;
                MethodPosition methodPosition = new MethodPosition(new TypePosition(filePosition, typeName), methodName);
                warnings.Add(methodPosition, WarningType.MissingAnnotation,
                    "Missing method documentation.");
            }
        }

    }
}