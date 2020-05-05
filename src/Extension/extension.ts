import * as path from 'path';
import { window, workspace, env, ExtensionContext } from 'vscode';
import * as os from "os"

import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
  Executable
} from 'vscode-languageclient';
import FeatureView from './views/FeatureView';

let client: LanguageClient;

export async function activate(context: ExtensionContext) {
  
  // The debug options for the server
  // --inspect=6009: runs the server in Node's Inspector mode so VS Code can attach to the server for debugging
  // let debugOptions = { execArgv: ['--nolazy', '--inspect=6009'] };

  // Determine platform language server platform path.
  let platform = os.platform();

  let build = "win-x64"
  let exeName = "AutoStep.LanguageServer.exe";

  if (platform != "win32")
  {
    // Something else, use the linux one?
    build = "linux-x64";
    exeName = "AutoStep.LanguageServer";
  }

  // If the extension is launched in debug mode then the debug server options are used
  // Otherwise the run options are used
  let runCommand: Executable = {    
    command: context.asAbsolutePath(path.join('server', build, exeName)),
    args: []
  };

  let debugCommand: Executable = {
    command: context.asAbsolutePath(path.join('server', build, exeName)),
    args: ["debug"]
  };

  let serverOptions: ServerOptions = { 
     run: runCommand,
     debug: debugCommand
  };

  // Options to control the language client
  let clientOptions: LanguageClientOptions = {
    // Register the server for plain text documents
    documentSelector: [{ scheme: 'file', language: 'autostep' }, {scheme: 'file', language: 'autostep-interaction' }],
    synchronize: {
      configurationSection: "autostep",
      fileEvents: [ 
        workspace.createFileSystemWatcher("**/*.as"), 
        workspace.createFileSystemWatcher("**/*.asi"), 
        workspace.createFileSystemWatcher("**/*.json")
      ]
    }
  };

  // Create the language client and start the client.
  client = new LanguageClient(
    'autostep',
    'AutoStep Language Server',
    serverOptions,
    clientOptions
  );

  // Start the client. This will also launch the server
  client.start();

  var featureView = new FeatureView(client);

  window.registerTreeDataProvider('autostep-features', featureView);

  await client.onReady();

  client.onNotification("autostep/build_complete", () => {
     featureView.refresh(); 
  });
}


export function deactivate(): Thenable<void> {
    return client.stop();
}