﻿using CommandLine;

namespace MoaiUtils.CreateApiDescription {
    public class Configuration {
        [Option('i', "input", Required = true,
            HelpText = "The Moai src directory")]
        public string InputDirectory { get; set; }

        [Option('o', "output", Required = true,
            HelpText = "The output directory where the code completion file(s) will be created")]
        public string OutputDirectory { get; set; }

        [Option('f', "format",
            HelpText = "The export format. Valid options are ZeroBrane (default) or SublimeText.")]
        public ExportFormat ExportFormat { get; set; }
    }
}
