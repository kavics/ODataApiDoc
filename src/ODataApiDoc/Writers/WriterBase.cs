using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ODataApiDoc.Writers
{
    internal abstract class WriterBase
    {
        // ReSharper disable once InconsistentNaming
        protected static readonly string CR = Environment.NewLine;

        public abstract void WriteTable(string title, OperationInfo[] ops, TextWriter output, Options options);

        public abstract void WriteOperation(OperationInfo op, TextWriter output, Options options);

        public virtual void WriteAttribute(string name, List<string> values, string prefix, TextWriter output)
        {
            if (values.Count == 0)
                return;
            values = values.Select(x => x.Replace(prefix, string.Empty)).ToList();
            output.WriteLine("- **{0}**: {1}", name, string.Join(", ", values));
        }

        public void WriteOperations(IEnumerable<OperationInfo> operations, string outputDir, Options options)
        {
            var fileWriters = new Dictionary<string, TextWriter>();

            foreach (var op in (operations))
            {
                try
                {
                    var categoryWriter = GetOrCreateWriter(outputDir, GetOutputFile(op), fileWriters);
                    WriteOperation(op, categoryWriter, options);
                }
                catch// (Exception e)
                {
                    //UNDONE: handle errors
                }
            }

            foreach (var fileWriter in fileWriters.Values)
            {
                fileWriter.Flush();
                fileWriter.Close();
            }
        }

        protected string GetOutputFile(OperationInfo op)
        {
            var name = op.Category ?? "uncategorized";
            return name + ".md";
        }

        protected TextWriter GetOrCreateWriter(string outDir, string outFile, Dictionary<string, TextWriter> writers)
        {
            if (!writers.TryGetValue(outFile, out var writer))
            {
                writer = new StreamWriter(Path.Combine(outDir, outFile), false);
                writers.Add(outFile, writer);
            }

            return writer;
        }

    }
}
