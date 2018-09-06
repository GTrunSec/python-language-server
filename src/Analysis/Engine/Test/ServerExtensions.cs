﻿// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using TestUtilities;

namespace Microsoft.PythonTools.Analysis {
    internal static class ServerExtensions {
        public static async Task<Server> InitializeAsync(this Server server, InterpreterConfiguration configuration, params string[] searchPaths) {
            configuration.AssertInstalled();

            server.OnLogMessage += Server_OnLogMessage;
            var properties = new InterpreterFactoryCreationOptions {
                TraceLevel = System.Diagnostics.TraceLevel.Verbose,
                DatabasePath = TestData.GetAstAnalysisCachePath(configuration.Version)
            }.ToDictionary();

            configuration.WriteToDictionary(properties);

            await server.Initialize(new InitializeParams {
                initializationOptions = new PythonInitializationOptions {
                    interpreter = new PythonInitializationOptions.Interpreter {
                        assembly = typeof(AstPythonInterpreterFactory).Assembly.Location,
                        typeName = typeof(AstPythonInterpreterFactory).FullName,
                        properties = properties
                    },
                    analysisUpdates = true,
                    searchPaths = searchPaths,
                    traceLogging = true,
                },
                capabilities = new ClientCapabilities {
                    python = new PythonClientCapabilities {
                        liveLinting = true,
                    }
                }
            });
            
            return server;
        }

        public static Task<ModuleAnalysis> GetAnalysisAsync(this Server server, Uri uri, int waitingTimeout = -1, int failAfter = 30000)
            => ((ProjectEntry)server.ProjectFiles.GetEntry(uri)).GetAnalysisAsync(waitingTimeout, new CancellationTokenSource(failAfter).Token);

        public static void EnqueueItem(this Server server, Uri uri) 
            => server.EnqueueItem((IDocument)server.ProjectFiles.GetEntry(uri));

        public static void EnqueueItems(this Server server, params IDocument[] projectEntries) {
            foreach (var document in projectEntries) {
                server.EnqueueItem(document);
            }
        }

        // TODO: Replace usages of AddModuleWithContent with OpenDefaultDocumentAndGetUriAsync
        public static ProjectEntry AddModuleWithContent(this Server server, string moduleName, string relativePath, string content) {
            var entry = (ProjectEntry)server.Analyzer.AddModule(moduleName, TestData.GetTestSpecificPath(relativePath));
            entry.ResetDocument(0, content);
            server.EnqueueItem(entry);
            return entry;
        }

        public static Task<CompletionList> SendCompletion(this Server server, Uri uri, int line, int character) {
            return server.Completion(new CompletionParams {
                textDocument = new TextDocumentIdentifier {
                    uri = uri
                },
                position = new Position {
                    line = line,
                    character = character
                }
            });
        }

        public static Task<SignatureHelp> SendSignatureHelp(this Server server, Uri uri, int line, int character) {
            return server.SignatureHelp(new TextDocumentPositionParams {
                textDocument = uri,
                position = new Position {
                    line = line,
                    character = character
                }
            });
        }

        public static Task<Reference[]> SendFindReferences(this Server server, Uri uri, int line, int character, bool includeDeclaration = true) {
            return server.FindReferences(new ReferencesParams {
                textDocument = uri,
                position = new Position {
                    line = line,
                    character = character
                },
                context = new ReferenceContext {
                    includeDeclaration = includeDeclaration,
                    _includeValues = true // For compatibility with PTVS
                }
            });
        }

        public static async Task<Uri> OpenDefaultDocumentAndGetUriAsync(this Server server, string content) {
            var uri = TestData.GetDefaultModuleUri();
            await server.SendDidOpenTextDocument(uri, content);
            return uri;
        }

        public static async Task SendDidOpenTextDocument(this Server server, Uri uri, string content, string languageId = null) {
            await server.DidOpenTextDocument(new DidOpenTextDocumentParams {
                textDocument = new TextDocumentItem {
                    uri = uri,
                    text = content,
                    languageId = languageId ?? "python"
                }
            });
        }

        public static async Task<ModuleAnalysis> OpenDefaultDocumentAndGetAnalysisAsync(this Server server, string content, int failAfter = 30000, string languageId = null) {
            var cancellationToken = new CancellationTokenSource(failAfter).Token;
            await server.SendDidOpenTextDocument(TestData.GetDefaultModuleUri(), content, languageId);
            cancellationToken.ThrowIfCancellationRequested();
            var projectEntry = (ProjectEntry) server.ProjectFiles.All.Single();
            return await projectEntry.GetAnalysisAsync(cancellationToken: cancellationToken);
        }

        public static void SendDidChangeTextDocument(this Server server, Uri uri, string text) {
            server.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = uri
                }, 
                contentChanges = new [] {
                    new TextDocumentContentChangedEvent {
                        text = text,
                    }, 
                }
            });
        }

        public static async Task<ModuleAnalysis> ChangeDefaultDocumentAndGetAnalysisAsync(this Server server, string text, int failAfter = 30000) {
            var projectEntry = (ProjectEntry) server.ProjectFiles.All.Single();
            server.SendDidChangeTextDocument(projectEntry.DocumentUri, text);
            return await projectEntry.GetAnalysisAsync(cancellationToken: new CancellationTokenSource(failAfter).Token);
        }

        private static void Server_OnLogMessage(object sender, LogMessageEventArgs e) {
            switch (e.type) {
                case MessageType.Error: Trace.TraceError($"[{TestEnvironmentImpl.Elapsed()}]: {e.message}"); break;
                case MessageType.Warning: Trace.TraceWarning($"[{TestEnvironmentImpl.Elapsed()}]: {e.message}"); break;
                case MessageType.Info: Trace.TraceInformation($"[{TestEnvironmentImpl.Elapsed()}]: {e.message}"); break;
                case MessageType.Log: Trace.TraceInformation($"[{TestEnvironmentImpl.Elapsed()}] LOG: {e.message}"); break;
            }
        }
    }
}