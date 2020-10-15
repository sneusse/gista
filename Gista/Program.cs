using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using ScottPlot;
using static Gista.Helpers;

namespace Gista
{
    class GistaPlot
    {
        private readonly string _configFile;
        private readonly Dictionary<string, string> _aliases = new Dictionary<string, string>();

        private readonly List<string> _excludeFilter = new List<string>();
        private readonly List<string> _forceInclude = new List<string>();

        private readonly Dictionary<string, Func<ICluster>> _clusterFactory = new Dictionary<string, Func<ICluster>>();

        private GitLogData _data;


        private StreamReader _sr;
        private string[] _tokens;
        private int _lnx = 0;
        private int _tx = 0;
        private int _skipCommits = 0;
        private double _daysPast = 10000;

        public GistaPlot(string configFile)
        {
            _configFile = configFile;
            ErrorNotFound(configFile);

            _clusterFactory["author"] = () => new ByAuthor(this);
        }

        public void ReadConfig()
        {
            Console.Error.WriteLine($"Loading config '{_configFile}'");
            _sr = new StreamReader(_configFile);
            _lnx = 0;


            MultiPlot mplot = null;
            Plot plot = null;
            string title = null;
            while (!_sr.EndOfStream)
            {
                Read();
                if (!HasToken || string.IsNullOrWhiteSpace(Token) || Token.StartsWith("#"))
                    continue;

                switch (Consume())
                {
                    case ":alias":
                    {
                        var realAuthor = Consume();
                        var aliases = ReadList();

                        foreach (var alias in aliases)
                        {
                            _aliases[alias] = realAuthor;
                        }

                        break;
                    }
                    case ":load":
                    {
                        var rawData = Consume();
                        _data = Needed(() => GitLogData.ParseRawStats(rawData, _aliases));
                        break;
                    }
                    case ":days":
                        _daysPast = double.Parse(Consume());
                        break;
                    case ":skip-commit":
                        _skipCommits = int.Parse(Consume());
                        break;
                    case ":include":
                        _forceInclude.AddRange(ReadList());
                        break;
                    case ":include-clear":
                        _forceInclude.Clear();
                        break;
                    case ":exclude":
                        _excludeFilter.AddRange(ReadList());
                        break;
                    case ":exclude-clear":
                        _excludeFilter.Clear();
                        break;
                    case ":exclude-remove":
                        var list = ReadList().ToHashSet();
                        _excludeFilter.RemoveAll(s => list.Contains(s));
                        break;
                    case ":figure":
                    {
                        var wxh = Consume().Split('x').Select(int.Parse).ToArray();

                        if (HasToken)
                        {
                            var indizes = Consume().Split('-').Select(int.Parse).ToArray();
                            mplot = new MultiPlot(wxh[0], wxh[1], indizes[0], indizes[1]);
                            plot = null;
                        }
                        else
                        {
                            mplot = null;
                            plot = new Plot(wxh[0], wxh[1]);
                        }

                        break;
                    }
                    case ":subplot":
                    {
                        var indizes = Consume().Split('-').Select(int.Parse).ToArray();
                        var plt = mplot.GetSubplot(indizes[0] - 1, indizes[1] - 1);
                        DrawPlot(plt, ref title);
                        break;
                    }
                    case ":plot":
                    {
                        DrawPlot(plot, ref title);

                        break;
                    }
                    case ":save":
                    {
                        mplot?.SaveFig(Consume());
                        plot?.SaveFig(Consume());
                        break;
                    }
                    case ":title":
                        title = Consume();
                        break;
                }
            }
        }

        private void DrawPlot(Plot plot, ref string title)
        {
            switch (Consume())
            {
                case "bars":
                {
                    var cluster = _clusterFactory[Consume()]();
                    cluster.Crunch();
                    double d = 0.8 / NumTokensLeft;
                    for (int i = 0; i < NumTokensLeft; i++)
                    {
                        var series = cluster[Look(i)];
                        plot.PlotBar(cluster.XValues, series.Values,
                            showValues: true,
                            label: series.Label,
                            barWidth: d,
                            xOffset: d * i);
                    }

                    plot.Ticks(logScaleY: true);
                    plot.XTicks(cluster.XValues, cluster.XLabels);
                    plot.Legend(location: legendLocation.upperRight);
                    plot.Title(title);
                    break;
                }
            }
        }


        private void ErrorRead(string message = null)
        {
            message = $"Error @ line: {_lnx} in file {_configFile} - {message}";
            ErrorExit(message);
        }

        private string[] Read()
        {
            if (_sr.EndOfStream)
                ErrorRead("EOF");

            ++_lnx;
            _tx = 0;
            _tokens = _sr.ReadLine().Splitz();
            return _tokens;
        }

        private string[] ReadList()
        {
            List<string> list = new List<string>();
            for (;;)
            {
                Read();
                if (!IsString)
                    break;
                list.Add(Token);
            }

            return list.ToArray();
        }

        private string Next()
        {
            _tx++;
            return Token;
        }

        private string Consume()
        {
            var token = Token;
            _tx++;
            return token;
        }

        private string Look(int la = 1)
        {
            if (la + _tx > _tokens.Length && !Debugger.IsAttached)
            {
                ErrorRead();
            }

            return _tokens[_tx + la];
        }

        private int NumTokes => _tokens.Length;
        private int NumTokensLeft => NumTokes - _tx;
        private string Token => Look(0);
        private bool IsCommand => !IsEmpty && Token.StartsWith(":");
        private bool IsEmpty => NumTokes == 0;
        private bool IsString => !IsEmpty && !IsCommand;
        private bool HasToken => _tx < _tokens.Length;
        private bool HasNext => _tx + 1 < _tokens.Length;


        interface ICluster
        {
            double[] XValues { get; }
            string[] XLabels { get; }

            void Crunch();
            Series this[string stat] { get; }
        }

        class Series
        {
            public double[] Values { get; set; }
            public string Label { get; set; }
        }

        class ByAuthor : ICluster
        {
            private readonly GistaPlot _gista;
            private readonly Dictionary<string, Series> _stats = new Dictionary<string, Series>();

            public Series this[string stat] => _stats[stat];

            public string[] XLabels { get; }
            public double[] XValues { get; }

            public ByAuthor(GistaPlot gista)
            {
                _gista = gista;
                XLabels = _gista._data.Authors.Select(k => k.Name.Split('@').First()).ToArray();
                XValues = DataGen.Consecutive(XLabels.Length);
            }

            private Series Add(string stat, string label)
            {
                if (!_stats.ContainsKey(stat))
                {
                    _stats.Add(stat, new Series() {Values = new double[XValues.Length], Label = label});
                }

                return _stats[stat];
            }

            public void Crunch()
            {
                var data = _gista._data;
                var exclude = _gista._excludeFilter;
                var include = _gista._forceInclude;

                var auth = data.Authors.ToArray();
                var xs = DataGen.Consecutive(auth.Length);
                var validFiles = data.Files
                    .Where(k => !k.IsMove)
                    .Where(k => exclude.TrueForAll(filter => !k.Path.Contains(filter)))
                    .ToHashSet();

                if (include.Count > 0)
                {
                    var toAdd = data.Files
                        .Where(file => !file.IsMove)
                        .Where(k => include.TrueForAll(filter => k.Path.Contains(filter)));
                    foreach (var file in toAdd)
                    {
                        validFiles.Add(file);
                    }
                }

                var filesA = Add("files-changed", "Files changed");
                var commitA = Add("commits", "Commits");
                var locTotal = Add("lines-changed", "Lines changed");
                var locAdded = Add("lines-added", "Lines added");
                var locRemoved = Add("lines-deleted", "Lines removed");

                for (var index = 0; index < auth.Length; index++)
                {
                    var author = auth[index];
                    var commits = data.Commits
                        .Where(k => k.Author == author)
                        .Where(k => k.Changes.Any())
                        .Where(k => k.Changes.Any(change => validFiles.Contains(change.File)))
                        .Where(k => k.Timestamp.AddDays(_gista._daysPast) > DateTime.Now)
                        .OrderBy(k => k.Timestamp)
                        .Skip(_gista._skipCommits)
                        .ToArray();


                    var changes = commits
                        .SelectMany(c => c.Changes)
                        .Where(k => !k.IsMove)
                        .Where(k => validFiles.Contains(k.File)).ToArray();
                    var files = changes.GroupBy(k => k.File).Select(k => k.Key).ToArray();
                    var loc = changes.Select(k => k.Adds + k.Deletes).Sum();

                    filesA.Values[index] = files.Length;
                    commitA.Values[index] = commits.Length;
                    locTotal.Values[index] = loc;
                    locAdded.Values[index] = changes.Select(k => k.Adds).Sum();
                    locRemoved.Values[index] = changes.Select(k => k.Deletes).Sum();
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var gistaplot = new GistaPlot("gista.cfg");
            gistaplot.ReadConfig();

            return;
        }

        private static void PrintStats(GitLogData data, List<string> excludeFilter)
        {
        }
    }
}