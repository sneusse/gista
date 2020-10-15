# gista
Git statistics generator

* this is a WIP

# preparation

* generate a git log to parse: `$ git log --since=1.year --numstat --pretty="%x00 %ae %x00 %aI %x00 %H %x00 %s" > raw.log`

# write a config

```conf

# Aliases for authors
:alias "sneusse" 
"my.mail@my.server.com"
"my.mail@my.other.server.com" 

# load your exported gitlog
:load "raw.log"

# limit the days (now - X)
:days 300

# exclude files from statistics (commits without changed files or moves will be ignored)
:exclude
".gen"

# for now, the parser is pretty basic and needs a newline after the list

# create a figure 1600px/800px, 2 rows, 1 column
:figure 1600x800 2-1

# title for the plot. It will be reused until its reset
:title "Total code contribution"

# plot a bars plot (the only available plot atm.)
# plot over "authors"
# available statisics for now: "files-changed" "commits" "lines-changed" "lines-added" "lines-deleted"
# there might be more in the future
:subplot 1-1 bars author files-changed commits

:title "Changed line statistics excluding .json files"

# include forcefully includes all matched files and is computed after exclude
:include 
".json"

# plot the "bigger numbers" in a separate diagram
:subplot 2-1 bars author lines-changed lines-added lines-deleted

# you can modify the include and exclude set anytime using:
#
# :exclude-clear
# :exclude
# :exclude-remove
# :include-clear
# :include
# :include-remove

:save "myplot.png"

```

