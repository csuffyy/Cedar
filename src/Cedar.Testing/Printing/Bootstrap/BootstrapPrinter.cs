﻿namespace Cedar.Testing.Printing.Bootstrap
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Inflector;

    public class BootstrapPrinter : IScenarioResultPrinter
    {
        private readonly TextWriter _output;
        private bool _disposed;
        private readonly IList<Tuple<Type, List<ScenarioResult>>> _tableOfContents;

        public BootstrapPrinter(Func<string, TextWriter> factory)
        {
            _output = factory(FileExtension);

            _tableOfContents = new List<Tuple<Type, List<ScenarioResult>>>();
        }

        public Task PrintResult(ScenarioResult result)
        {
            _tableOfContents.Last().Item2.Add(result);
            return Task.FromResult(0);
        }

        public Task PrintCategoryFooter(Type foundOn)
        {
            return Task.FromResult(0);
        }

        public Task PrintCategoryHeader(Type foundOn)
        {
            _tableOfContents.Add(Tuple.Create(foundOn, new List<ScenarioResult>()));
            
            return Task.FromResult(0);
        }

        public async Task Flush()
        {
            await _output.WriteLineAsync("<!DOCTYPE html>");
            await _output.WriteLineAsync("<html>");

            await WriteHead();
            await WriteBody();

            await _output.WriteLineAsync("</html>");

            await _output.FlushAsync();
        }

        public string FileExtension
        {
            get { return "html"; }
        }

        private async Task WriteHead()
        {
            await _output.WriteLineAsync("<head><meta charset='UTF-8'><style>");
            
            var stylesheet = GetType().Assembly.GetManifestResourceStream(GetType(), "bootstrap.css");
            
            using (var reader = new StreamReader(stylesheet))
            {
                await _output.WriteLineAsync(await reader.ReadToEndAsync());
            }
            
            await _output.WriteLineAsync("</style></head>");
        }

        private async Task WriteBody()
        {
            await _output.WriteLineAsync("<body><section class='container'>");
            await WriteNavigation();
            await WriteResults();
            await _output.WriteLineAsync("</section></body>");
        }

        private async Task WriteNavigation()
        {
            await _output.WriteLineAsync("<nav><ul>");
            foreach (var item in _tableOfContents)
            {
                var suiteName = item.Item1;

                var categoryId = suiteName.GetCategoryId();
                var categoryName = suiteName.GetCategoryName();
                var results = item.Item2;

                await _output.WriteLineAsync(
                        string.Format(
                            "<li><a href='#{0}'>{1}</a> ({2})</li>",
                            categoryId, categoryName, results.All(result => result.Passed) ? "PASSED" : "FAILED"));
            }
            await _output.WriteLineAsync("</ul></nav>");
            await _output.WriteLineAsync();
        }

        private async Task WriteResults()
        {
            foreach (var item in _tableOfContents)
            {
                var category = item.Item1;
                var results = item.Item2;

                await WriteCategoryHeader(category);

                foreach (var result in results)
                {
                    await WriteResult(result);
                }

                await WriteCategoryFooter();
            }
        }

        private async Task WriteResult(ScenarioResult result)
        {
            await _output.WriteLineAsync(string.Format("<div class='alert alert-{0}'>", result.Passed ? "success" : "danger"));
            await _output.WriteLineAsync("<details id='{0}'>");
            await _output.WriteLineAsync("<summary>" + (result.Name ?? "???").Underscore().Titleize() + " - " + (result.Passed ? "Passed" : "Failed") + "</summary>");
            await _output.WriteLineAsync("<pre>");
            await WriteGiven(result.Given);
            await WriteWhen(result.When);
            await WriteExpect(result.Expect);
            await WriteResults(result.Results);
            await _output.WriteLineAsync("</pre>");
            await _output.WriteLineAsync("</details>");
            await _output.WriteLineAsync("</div>");
        }

        private async Task WriteCategoryHeader(Type foundOn)
        {
            await _output.WriteLineAsync(string.Format("<section id='{0}'>", foundOn.GetCategoryId()));
            await _output.WriteLineAsync(string.Format("<h1>{0}</h1>", foundOn.GetCategoryName()));
        }

        private async Task WriteCategoryFooter()
        {
            await _output.WriteLineAsync("</section>");
        }

        private Task WriteGiven(object given)
        {
            return WriteSection("Given", given);
        }

        private Task WriteWhen(object when)
        {
            return WriteSection("When", when);
        }

        private Task WriteExpect(object expect)
        {
            return WriteSection("Expect", expect);
        }

        private Task WriteResults(object expect)
        {
            return WriteSection("Results", expect);
        }

        private async Task WriteSection(string sectionName, object section, string prefix = "\t")
        {
            await _output.WriteLineAsync(sectionName + ":");
            foreach (var line in section.NicePrint(prefix))
            {
                await _output.WriteLineAsync(line);
            }
            await _output.WriteLineAsync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _output.Dispose();
        }
    }
}