using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KMeanLibrary
{
    /// <summary>
    /// Provides a simple implementation of the k-Means algorithm. This solution is quite simple and does not support any parallel execution as of yet.
    /// </summary>
    public static class KMeans
    {
        private static double[][] ConvertEntities<T>(IEnumerable<T> items)
        {
            var type = typeof(T);
            var data = new List<double[]>();

            // If the type is an array type
            if (type.IsArray && type.IsAssignableFrom(typeof(double[])))
            {
                data.AddRange(items.Select(item => item as double[]));

                return data.ToArray();
            }

            var getters = new List<MethodInfo>();

            // Iterate over the type and extract all the properties that have the KMeansValueAttribute set and use them as attributes
            var attribType = typeof(KMeansValueAttribute);

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var attribs = property.GetCustomAttributes(attribType, false).OfType<KMeansValueAttribute>().ToArray();
                if (attribs.Length <= 0) continue;

                var getter = property.GetGetMethod();
                if (getter == null)
                    throw new InvalidOperationException("No public getter for property '" + property.Name +
                                                        "'. All properties marked with the KMeansValueAttribute must have a public getter");

                if (!property.PropertyType.IsAssignableFrom(typeof(double)) && !property.PropertyType.IsAssignableFrom(typeof(int)) &&
                    !property.PropertyType.IsAssignableFrom(typeof(float)) && !property.PropertyType.IsAssignableFrom(typeof(long)) &&
                    !property.PropertyType.IsAssignableFrom(typeof(decimal)) && !property.PropertyType.IsAssignableFrom(typeof(short)))
                    throw new InvalidOperationException("Property type '" + property.PropertyType.Name + "' for property '" + property.Name +
                                                        "' cannot be assigned to System.Double. ");

                getters.Add(getter);
            }

            foreach (var item in items)
            {
                var values = new List<double>(getters.Count);

                values.AddRange(getters.Select(getter => Convert.ToDouble(getter.Invoke(item, null))));

                data.Add(values.ToArray());
            }

            return data.ToArray();
        }

        /// <summary>
        /// Clusters the given item set into the desired number of clusters. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items">the list of data items that should be processed, this can be an array of primitive values such as <see cref="System.Double[]"/> 
        /// or a class struct that exposes properties using the <see cref="KMeansValueAttribute"/></param>
        /// <param name="clusterCount">the desired number of clusters</param>
        /// <param name="maxIterations">the maximum number of iterations to perform</param>
        /// <param name="calculateDistanceFunction">optional, custom distance function, if omitted then the euclidean distance will be used as default</param>
        /// <param name="randomSeed">optional, a seed for the random generator that initially arranges the clustering of the nodes (specify the same value to ensure that the start ordering will be the same)</param>
        /// <param name="initialCentroidIndices">optional, the initial centroid configuration (as indicies into the <see cref="items"/> array). When this is used the <see cref="randomSeed"/> has no effect.
        /// Experiment with this as the initial arrangements of the centroids has a huge impact on the final cluster arrangement.</param>
        /// <returns>a result containing the items arranged into clusters as well as the centroids converged on and the total distance value for the cluster nodes.</returns>
        public static KMeansResults<T> Cluster<T>(T[] items, int clusterCount, int maxIterations,
            KMeansCalculateDistanceDelegate calculateDistanceFunction = null, int randomSeed = 0, int[] initialCentroidIndices = null)
        {
            var data = ConvertEntities(items);

            // Use the built in Euclidean distance calculation if no custom one is specified
            calculateDistanceFunction ??= CalculateDistance;

            var hasChanges = true;
            var iteration = 0;
            double totalDistance = 0;
            var numData = data.Length;
            var numAttributes = data[0].Length;

            // Create a random initial clustering assignment
            var clustering = InitializeClustering(numData, clusterCount, randomSeed);

            // Create cluster means and centroids
            var means = CreateMatrix(clusterCount, numAttributes);
            var centroidIdx = new int[clusterCount];
            var clusterItemCount = new int[clusterCount];

            // If we specify initial centroid indices then let's assign clustering based on those immediately
            if (initialCentroidIndices != null && initialCentroidIndices.Length == clusterCount)
            {
                centroidIdx = initialCentroidIndices;
                AssignClustering(data, clustering, centroidIdx, clusterCount, calculateDistanceFunction);
            }

            // Perform the clustering
            while (hasChanges && iteration < maxIterations)
            {
                clusterItemCount = new int[clusterCount];
                totalDistance = CalculateClusteringInformation(data, clustering, ref means, ref centroidIdx, clusterCount, ref clusterItemCount,
                    calculateDistanceFunction);

                hasChanges = AssignClustering(data, clustering, centroidIdx, clusterCount, calculateDistanceFunction);
                ++iteration;
            }

            // Create the final clusters
            var clusters = new T[clusterCount][];
            for (var k = 0; k < clusters.Length; k++) clusters[k] = new T[clusterItemCount[k]];

            var clustersCurIdx = new int[clusterCount];
            for (var i = 0; i < clustering.Length; i++)
            {
                clusters[clustering[i]][clustersCurIdx[clustering[i]]] = items[i];
                ++clustersCurIdx[clustering[i]];
            }

            // Return the results
            return new KMeansResults<T>(clusters, means, centroidIdx, totalDistance);
        }

        private static int[] InitializeClustering(int numData, int clusterCount, int seed)
        {
            var rnd = new Random(seed);
            var clustering = new int[numData];

            for (var i = 0; i < numData; ++i) clustering[i] = rnd.Next(0, clusterCount);

            return clustering;
        }

        private static double[][] CreateMatrix(int rows, int columns)
        {
            var matrix = new double[rows][];

            for (var i = 0; i < matrix.Length; i++) matrix[i] = new double[columns];

            return matrix;
        }

        private static double CalculateClusteringInformation(IReadOnlyList<double[]> data, IReadOnlyList<int> clustering, ref double[][] means,
            ref int[] centroidIdx, int clusterCount, ref int[] clusterItemCount, KMeansCalculateDistanceDelegate calculateDistanceFunction)
        {
            // Reset the means to zero for all clusters
            foreach (var mean in means)
                for (var i = 0; i < mean.Length; i++)
                    mean[i] = 0;

            // Calculate the means for each cluster
            // Do this in two phases, first sum them all up and then divide by the count in each cluster
            for (var i = 0; i < data.Count; i++)
            {
                // Sum up the means
                var row = data[i];
                var clusterIdx = clustering[i]; // What cluster is data i assigned to
                ++clusterItemCount[clusterIdx]; // Increment the count of the cluster that row i is assigned to
                for (var j = 0; j < row.Length; j++) means[clusterIdx][j] += row[j];
            }

            // Now divide to get the average
            for (var k = 0; k < means.Length; k++)
            {
                for (var a = 0; a < means[k].Length; a++)
                {
                    var itemCount = clusterItemCount[k];
                    means[k][a] /= itemCount > 0 ? itemCount : 1;
                }
            }

            double totalDistance = 0;

            // Calc the centroids
            var minDistances = new double[clusterCount].Select(x => double.MaxValue).ToArray();

            for (var i = 0; i < data.Count; i++)
            {
                var clusterIdx = clustering[i]; // What cluster is data i assigned to
                //var distance = CalculateDistance(data[i], means[clusterIdx]);
                var distance = calculateDistanceFunction(data[i], means[clusterIdx]);
                totalDistance += distance;

                if (!(distance < minDistances[clusterIdx])) continue;

                minDistances[clusterIdx] = distance;
                centroidIdx[clusterIdx] = i;
            }
            //double totalCentroidDistance = minDistances.Sum();

            return totalDistance;
        }

        /// <summary>
        /// Calculates the distance for each point in <see cref="data"/> from each of the centroid in <see cref="centroidIdx"/> and 
        /// assigns the data item to the cluster with the minimum distance.
        /// </summary>
        /// <returns>true if any clustering arrangement has changed, false if clustering did not change.</returns>
        private static bool AssignClustering(IReadOnlyList<double[]> data, IList<int> clustering, IReadOnlyList<int> centroidIdx, int clusterCount,
            KMeansCalculateDistanceDelegate calculateDistanceFunction)
        {
            var changed = false;

            for (var i = 0; i < data.Count; i++)
            {
                var minDistance = double.MaxValue;
                var minClusterIndex = -1;

                for (var k = 0; k < clusterCount; k++)
                {
                    var distance = calculateDistanceFunction(data[i], data[centroidIdx[k]]);

                    if (!(distance < minDistance)) continue;

                    minDistance = distance;
                    minClusterIndex = k;
                    // todo: track outliers here as well and maintain an average and std calculation for the distances!
                }

                // Re-arrange the clustering for datapoint if needed
                if (minClusterIndex == -1 || clustering[i] == minClusterIndex) continue;

                changed = true;
                clustering[i] = minClusterIndex;
            }

            return changed;
        }

        /// <summary>
        ///  Calculates the eculidean distance from the <see cref="point"/> to the <see cref="centroid"/>
        /// </summary>
        private static double CalculateDistance(double[] point, double[] centroid)
        {
            // For each attribute calculate the squared difference between the centroid and the point
            var sum = point.Select((t, i) => Math.Pow(centroid[i] - t, 2)).Sum();

            return Math.Sqrt(sum);
        }
    }
}