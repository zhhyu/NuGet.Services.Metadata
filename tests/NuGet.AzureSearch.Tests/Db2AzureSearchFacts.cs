using System.Collections.Generic;
using NuGet.AzureSearch;
using Xunit;
using PackageRegistrationKeyAndPackageCount = NuGet.AzureSearch.Db2AzureSearch.PackageRegistrationKeyAndPackageCount;

namespace NuGet.AzureSearchs
{
    public class Db2AzureSearchFacts
    {
        public class GroupKeyRanges
        {
            [Fact]
            public void CreatesRanges()
            {
                var input = new List<PackageRegistrationKeyAndPackageCount>
                {
                    new PackageRegistrationKeyAndPackageCount(1, 500),
                    new PackageRegistrationKeyAndPackageCount(2, 498),
                    new PackageRegistrationKeyAndPackageCount(3, 1),
                    new PackageRegistrationKeyAndPackageCount(4, 1), // End of batch 0
                    new PackageRegistrationKeyAndPackageCount(5, 1),
                    new PackageRegistrationKeyAndPackageCount(6, 500), // End of batch 1
                    new PackageRegistrationKeyAndPackageCount(7, 500),
                    new PackageRegistrationKeyAndPackageCount(8, 400), // End of batch 2
                };

                var output = Db2AzureSearch.GroupKeyRanges(input);

                Assert.Equal(3, output.Count);

                Assert.Equal(1, output[0].BeginKey);
                Assert.Equal(5, output[0].EndKey);
                Assert.Equal(1000, output[0].PackageCount);

                Assert.Equal(5, output[1].BeginKey);
                Assert.Equal(7, output[1].EndKey);
                Assert.Equal(501, output[1].PackageCount);

                Assert.Equal(7, output[2].BeginKey);
                Assert.Equal(9, output[2].EndKey);
                Assert.Equal(900, output[2].PackageCount);
            }

            [Fact]
            public void AllowsBatchSizeLargerThanMaxForSingleKeyAndOtherSmallBatches()
            {
                var input = new List<PackageRegistrationKeyAndPackageCount>
                {
                    new PackageRegistrationKeyAndPackageCount(1, 3), // End of batch 0
                    new PackageRegistrationKeyAndPackageCount(2, 998), // End of batch 1
                    new PackageRegistrationKeyAndPackageCount(3, 1000), // End of batch 2
                    new PackageRegistrationKeyAndPackageCount(4, 1001), // End of batch 3
                    new PackageRegistrationKeyAndPackageCount(5, 1), // End of batch 4
                };

                var output = Db2AzureSearch.GroupKeyRanges(input);

                Assert.Equal(5, output.Count);

                Assert.Equal(1, output[0].BeginKey);
                Assert.Equal(2, output[0].EndKey);
                Assert.Equal(3, output[0].PackageCount);

                Assert.Equal(2, output[1].BeginKey);
                Assert.Equal(3, output[1].EndKey);
                Assert.Equal(998, output[1].PackageCount);

                Assert.Equal(3, output[2].BeginKey);
                Assert.Equal(4, output[2].EndKey);
                Assert.Equal(1000, output[2].PackageCount);

                Assert.Equal(4, output[3].BeginKey);
                Assert.Equal(5, output[3].EndKey);
                Assert.Equal(1001, output[3].PackageCount);

                Assert.Equal(5, output[4].BeginKey);
                Assert.Equal(6, output[4].EndKey);
                Assert.Equal(1, output[4].PackageCount);
            }

            [Fact]
            public void AllowsBatchSizeLargerThanMaxForSingleKeyButOnlyOneBatch()
            {
                var input = new List<PackageRegistrationKeyAndPackageCount>
                {
                    new PackageRegistrationKeyAndPackageCount(1, 1001), // End of batch 0
                };

                var output = Db2AzureSearch.GroupKeyRanges(input);

                Assert.Equal(1, output.Count);

                Assert.Equal(1, output[0].BeginKey);
                Assert.Equal(2, output[0].EndKey);
                Assert.Equal(1001, output[0].PackageCount);
            }

            [Fact]
            public void AllowsOneSmallBatch()
            {
                var input = new List<PackageRegistrationKeyAndPackageCount>
                {
                    new PackageRegistrationKeyAndPackageCount(1, 5), // End of batch 0
                };

                var output = Db2AzureSearch.GroupKeyRanges(input);

                Assert.Equal(1, output.Count);

                Assert.Equal(1, output[0].BeginKey);
                Assert.Equal(2, output[0].EndKey);
                Assert.Equal(5, output[0].PackageCount);
            }

            [Fact]
            public void AllowsGapsInRegistrationKeys()
            {
                var input = new List<PackageRegistrationKeyAndPackageCount>
                {
                    new PackageRegistrationKeyAndPackageCount(1, 500),
                    new PackageRegistrationKeyAndPackageCount(11, 498),
                    new PackageRegistrationKeyAndPackageCount(21, 1),
                    new PackageRegistrationKeyAndPackageCount(31, 1), // End of batch 0
                    new PackageRegistrationKeyAndPackageCount(41, 1),
                    new PackageRegistrationKeyAndPackageCount(51, 500), // End of batch 1
                    new PackageRegistrationKeyAndPackageCount(61, 500),
                    new PackageRegistrationKeyAndPackageCount(71, 400), // End of batch 2
                };

                var output = Db2AzureSearch.GroupKeyRanges(input);

                Assert.Equal(3, output.Count);

                Assert.Equal(1, output[0].BeginKey);
                Assert.Equal(41, output[0].EndKey);
                Assert.Equal(1000, output[0].PackageCount);

                Assert.Equal(41, output[1].BeginKey);
                Assert.Equal(61, output[1].EndKey);
                Assert.Equal(501, output[1].PackageCount);

                Assert.Equal(61, output[2].BeginKey);
                Assert.Equal(72, output[2].EndKey);
                Assert.Equal(900, output[2].PackageCount);
            }
        }
    }
}
