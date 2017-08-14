# Project Name

CodeSearch

## Purpose
CodeSearch will connect to a TFS repository of your designation, and will get all files that match a predefined set of extensions from all team project collections in that repo. It will then build queryable indices over the whole fileset to enable cross-repo searches using Lucene syntax.

## Installation

Build the solution. The installer project will generate "codesearch.exe" under the Installer subfolder, and you can run it from there.
The installer will add two Windows services to your host computer: Codesearch Indexer Service and Codesearch Webhost Service. These can be run and 
restarted individually, and will update indices and serve query results, respectively.

The main search form can be accessed at http://servername:8102/ after successful installation and when the search index is built.
Logs are in the Logs subfolder under the installation directory.

## Usage

Access search form at http://servername:8102/ after code indexing is complete, and submit your query

## Contributing

1. Fork it!
2. Create your feature branch: `git checkout -b my-new-feature`
3. Commit your changes: `git commit -am 'Add some feature'`
4. Push to the branch: `git push origin my-new-feature`
5. Submit a pull request :D

## History

11 Aug 2017: Initial push

## Credits

Original author: Lars Yde (larsyde@gmail.com)

## License

MIT License

Copyright (c) 2017 Lars Yde

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
