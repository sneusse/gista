using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Gista
{
    class GitLogData
    {
        private enum ParseState
        {
            Summary,
            LoC
        }

        public static GitLogData ParseRawStats(string rawStats, IDictionary<string, string> authorAlias = null)
        {
            var data = new GitLogData();
            using var fs = System.IO.File.OpenText(rawStats);
            var lineNr = 0;
            var state = ParseState.Summary;

            GitLogData.Commit currentCommit = null;


            while (!fs.EndOfStream)
            {
                ++lineNr;
                var line = fs.ReadLine();

                void ExpectOrFail(char c, char expected)
                {
                    if (c != expected)
                        Helpers.ErrorExit($"Expected {expected} @ line {lineNr}. Found: {c}");
                }

                if (line.Length == 0 || String.IsNullOrWhiteSpace(line))
                    continue;

                state = line[0] == '\0' ? ParseState.Summary : ParseState.LoC;

                try
                {
                    switch (state)
                    {
                        case ParseState.Summary:
                        {
                            var lineData = line.Split('\0');
                            var mail = lineData[1].Trim();
                            var date = DateTime.Parse(lineData[2].Trim());
                            var hash = lineData[3].Trim();
                            var summary = lineData[4].Trim();

                            currentCommit = data.GetCommit(hash);

                            if (authorAlias != null && authorAlias.ContainsKey(mail))
                            {
                                mail = authorAlias[mail];
                            }

                            currentCommit.Author = data.GetAuthor(mail);
                            currentCommit.Summary = summary;
                            currentCommit.Timestamp = date;
                                
                            break;
                        }
                        case ParseState.LoC:
                        {
                            var lineData = line.Split('\t');

                            int adds = 0;
                            int dels = 0;

                            Int32.TryParse(lineData[0], out adds);
                            Int32.TryParse(lineData[1], out dels);

                            var file = data.GetFile(lineData[2]);

                            currentCommit.AddChange(file, adds, dels);

                            break;
                        }

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    Helpers.ErrorExit($"Stats parsing failed @ line {line}. State. {state}");
                }
            }

            return data;
        }

        readonly ConcurrentDictionary<string, File> _files = new ConcurrentDictionary<string, File>();
        readonly ConcurrentDictionary<string, Author> _authors = new ConcurrentDictionary<string, Author>();
        readonly ConcurrentDictionary<string, Commit> _commits = new ConcurrentDictionary<string, Commit>();

        public IEnumerable<File> Files => _files.Values;
        public IEnumerable<Author> Authors => _authors.Values.OrderBy(k => k.Name);

        public IEnumerable<Commit> Commits => _commits.Values;

        public class Author
        {
            public Author(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        public class File
        {
            public string Path { get; }
            public bool IsMove => Path.Contains(" => ");

            public File(string path)
            {
                Path = path;
            }
        }

        public class Change
        {
            public File File { get; }


            public Change(File file, in int adds, in int deletes)
            {
                File = file;
                Adds = adds;
                Deletes = deletes;
            }

            public bool IsMove => File.Path.Contains(" => ");

            public int Adds { get; set; }
            public int Deletes { get; set; }
        }

        public class Commit
        {
            private readonly List<Change> _changes = new List<Change>();

            public Commit(string hash)
            {
                Hash = hash;
            }

            public Author Author { get; set; }
            public DateTime Timestamp { get; set; }
            public string Hash { get; }
            public string Summary { get; set; }

            public IEnumerable<File> Files => Changes.Select(k => k.File);

            public IEnumerable<Change> Changes => _changes;

            public void AddChange(File file, int adds = 0, int deletes = 0)
            {
                _changes.Add(new Change(file, adds, deletes));
            }
        }

        public File GetFile(string path) => _files.GetOrAdd(path, s => new File(s));
        public Commit GetCommit(string hash) => _commits.GetOrAdd(hash, s => new Commit(s));
        public Author GetAuthor(string mail) => _authors.GetOrAdd(mail, s => new Author(mail));
    }
}