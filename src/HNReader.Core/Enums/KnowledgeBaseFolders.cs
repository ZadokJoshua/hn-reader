using HNReader.Core.Constants;
using System.ComponentModel;

namespace HNReader.Core.Enums;

public enum KnowledgeBaseFolders
{
    [Description(AppFolderNames.NEWS_DIGEST_FOLDER)]
    NewsDigest,
    [Description(AppFolderNames.STORIES_FOLDER)]
    Stories
}
