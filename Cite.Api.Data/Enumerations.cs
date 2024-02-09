// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

namespace Cite.Api.Data.Enumerations
{
    public enum ItemStatus
    {
        Pending = 10,
        Active = 20,
        Cancelled = 30,
        Complete = 40,
        Archived = 50
    }

    public enum RightSideDisplay
    {
        ScoreSummary = 0,
        HtmlBlock = 10,
        EmbeddedUrl = 20,
        Scoresheet = 30,
        None = 40
    }

}

