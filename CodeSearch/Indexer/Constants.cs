using System;

namespace Indexer
{
    public static class Constants
    {
        public static string[] Exceptions = { }; //{ @"vnext", @"-oem" };
        public static string[] Exclusions = { };// { @"/development/", @"/release/", @"/team/" };
        public static TimeSpan MaxTfsItemAge = TimeSpan.FromHours(6.0);
    }
}