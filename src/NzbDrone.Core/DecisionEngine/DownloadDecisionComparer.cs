using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public class DownloadDecisionComparer : IComparer<DownloadDecision>
    {
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;

        public delegate int CompareDelegate(DownloadDecision x, DownloadDecision y);
        public delegate int CompareDelegate<TSubject, TValue>(DownloadDecision x, DownloadDecision y);

        public DownloadDecisionComparer(IConfigService configService, IDelayProfileService delayProfileService)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
        }

        public int Compare(DownloadDecision x, DownloadDecision y)
        {
            var comparers = new List<CompareDelegate>
            {
                CompareQuality,
                CompareCustomFormatScore,
                CompareProtocol,
                CompareIndexerPriority,
                ComparePeersIfTorrent,
                CompareBookCount,
                CompareAgeIfUsenet,
                CompareSize
            };

            return comparers.Select(comparer => comparer(x, y)).FirstOrDefault(result => result != 0);
        }

        private int CompareBy<TSubject, TValue>(TSubject left, TSubject right, Func<TSubject, TValue> funcValue)
            where TValue : IComparable<TValue>
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var leftValue = funcValue(left);
            var rightValue = funcValue(right);

            if (leftValue == null && rightValue == null)
            {
                return 0;
            }

            if (leftValue == null)
            {
                return -1;
            }

            if (rightValue == null)
            {
                return 1;
            }

            return leftValue.CompareTo(rightValue);
        }

        private int CompareByReverse<TSubject, TValue>(TSubject left, TSubject right, Func<TSubject, TValue> funcValue)
            where TValue : IComparable<TValue>
        {
            return CompareBy(left, right, funcValue) * -1;
        }

        private int CompareAll(params int[] comparers)
        {
            return comparers.Select(comparer => comparer).FirstOrDefault(result => result != 0);
        }

        private int CompareIndexerPriority(DownloadDecision x, DownloadDecision y)
        {
            return CompareByReverse(x.RemoteBook?.Release, y.RemoteBook?.Release, release => release?.IndexerPriority ?? int.MinValue);
        }

        private int CompareQuality(DownloadDecision x, DownloadDecision y)
        {
            if (_configService.DownloadPropersAndRepacks == ProperDownloadTypes.DoNotPrefer)
            {
                return CompareBy(x.RemoteBook, y.RemoteBook, GetQualityIndex);
            }

            return CompareAll(
                CompareBy(x.RemoteBook, y.RemoteBook, GetQualityIndex),
                CompareBy(x.RemoteBook, y.RemoteBook, GetQualityRevision));
        }

        private int CompareCustomFormatScore(DownloadDecision x, DownloadDecision y)
        {
            return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook?.CustomFormatScore ?? 0);
        }

        private int CompareProtocol(DownloadDecision x, DownloadDecision y)
        {
            return CompareBy(x.RemoteBook, y.RemoteBook, IsPreferredProtocol);
        }

        private int CompareBookCount(DownloadDecision x, DownloadDecision y)
        {
            var discographyCompare = CompareBy(x.RemoteBook,
                y.RemoteBook,
                remoteBook => remoteBook?.ParsedBookInfo?.Discography ?? false);

            if (discographyCompare != 0)
            {
                return discographyCompare;
            }

            return CompareByReverse(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook?.Books?.Count ?? 0);
        }

        private int ComparePeersIfTorrent(DownloadDecision x, DownloadDecision y)
        {
            // Different protocols should get caught when checking the preferred protocol,
            // since we're dealing with the same series in our comparisions
            if (x.RemoteBook?.Release?.DownloadProtocol != DownloadProtocol.Torrent ||
                y.RemoteBook?.Release?.DownloadProtocol != DownloadProtocol.Torrent)
            {
                return 0;
            }

            return CompareAll(
                CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
                {
                    var seeders = remoteBook?.Release != null ? TorrentInfo.GetSeeders(remoteBook.Release) : null;

                    return seeders.HasValue && seeders.Value > 0 ? Math.Round(Math.Log10(seeders.Value)) : 0;
                }),
                CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
                {
                    var peers = remoteBook?.Release != null ? TorrentInfo.GetPeers(remoteBook.Release) : null;

                    return peers.HasValue && peers.Value > 0 ? Math.Round(Math.Log10(peers.Value)) : 0;
                }));
        }

        private int CompareAgeIfUsenet(DownloadDecision x, DownloadDecision y)
        {
            if (x.RemoteBook?.Release?.DownloadProtocol != DownloadProtocol.Usenet ||
                y.RemoteBook?.Release?.DownloadProtocol != DownloadProtocol.Usenet)
            {
                return 0;
            }

            return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
            {
                if (remoteBook?.Release == null)
                {
                    return 0;
                }

                var ageHours = remoteBook.Release.AgeHours;
                var age = remoteBook.Release.Age;

                if (ageHours < 1)
                {
                    return 1000;
                }

                if (ageHours <= 24)
                {
                    return 100;
                }

                if (age <= 7)
                {
                    return 10;
                }

                return 1;
            });
        }

        private int CompareSize(DownloadDecision x, DownloadDecision y)
        {
            // TODO: Is smaller better? Smaller for usenet could mean no par2 files.
            return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => (remoteBook?.Release?.Size ?? 0L).Round(200.Megabytes()));
        }

        private QualityIndex GetQualityIndex(RemoteBook remoteBook)
        {
            var parsedQuality = remoteBook?.ParsedBookInfo?.Quality;

            if (remoteBook?.Author?.QualityProfile?.Value == null || parsedQuality?.Quality == null)
            {
                return new QualityIndex(int.MinValue);
            }

            return remoteBook.Author.QualityProfile.Value.GetIndex(parsedQuality.Quality);
        }

        private Revision GetQualityRevision(RemoteBook remoteBook)
        {
            if (remoteBook?.ParsedBookInfo?.Quality == null)
            {
                return new Revision(0);
            }

            return remoteBook.ParsedBookInfo.Quality.Revision ?? new Revision(0);
        }

        private bool IsPreferredProtocol(RemoteBook remoteBook)
        {
            if (remoteBook?.Release == null || remoteBook?.Author?.Tags == null)
            {
                return false;
            }

            var delayProfile = _delayProfileService.BestForTags(remoteBook.Author.Tags);

            if (delayProfile == null)
            {
                return false;
            }

            return remoteBook.Release.DownloadProtocol == delayProfile.PreferredProtocol;
        }
    }
}
