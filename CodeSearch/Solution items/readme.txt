CodeSearch is free software and uses different open source and / or free technologies to achieve its objective of being an indexing service for TFS.
The list includes, but is not limited to, the following:

OpenGrok: available at https://github.com/OpenGrok/OpenGrok/
MadMilkMan.ini: available at https://github.com/MarioZ/MadMilkman.Ini 
Jetty: available at http://www.eclipse.org/jetty/
Team Foundation Server Client Libraries: available from https://www.visualstudio.com/en-us/docs/integrate/get-started/client-libraries/dotnet
NamedPipeWrapper: available at https://github.com/acdvorak/named-pipe-wrapper
TopShelf: available at http://topshelf-project.com/
NLog: available at http://nlog-project.org/
InsertIcons: available at https://github.com/einaregilsson/InsertIcons

CodeSearch (CS) installs to a folder of your designation and uses that location to store code files and index data. The amount of data and processing resoures consumed may
be significant, depending on your system. As a general rule of thumb, expect CS to consume appx 2.5 times the size of your codebase in disk space. So, a codebase of 40GB requires 100GB of available disk space.
Also, the time required to build the initial index may be considerable. Again, the exact time taken varies according to the size of your repository and the processing power of your host computer, but on average
expect several hours, potentially days for very large code repositories.

To do a search, go to http://<servername>:8102, where servername is the name of the computer you installed CodeSearch to. You can access the search form at any time after installation, but results will only be complete
once the initial index is built.

To access CS logs, go to the "logs" subfolder under the installation directory.
To access CS services, open "services.msc" from the Windows command prompt and look for "CodeSearch Indexer Service", which gets TFS files and updates indices, and "CodeSearch Webhost Service" which serves our the search results
and hosts the web forms. These services can be restarted and run independently.

For further questions, please contact the author at codesearchtfs@gmail.com

