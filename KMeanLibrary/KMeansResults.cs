namespace KMeanLibrary
{
    /// <summary>
    /// Represents a single result from the <see cref="KMeans"/> algorithm. 
    /// Contains the original items arranged into the clusters converged on as well as the centroids chosen and the total distance of the converged solution.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class KMeansResults<T>
    {
        /// <summary>
        /// The original items arranged into the clusters converged on
        /// </summary>
        public T[][] Clusters { get; private set; }

        /// <summary>
        /// The final mean values used for the clusters. Mostly for debugging purposes.
        /// </summary>
        public double[][] Means { get; private set; }

        /// <summary>
        /// The list of centroids used in the final solution. These are indicies into the original data.
        /// </summary>
        public int[] Centroids { get; private set; }

        /// <summary>
        /// The total distance between all the nodes and their centroids in the final solution. 
        /// This can be used as a reference point on how "good" the solution is when the algorithm is run repeatedly with different starting configuration.
        /// Lower is "usually" better.
        /// </summary>
        public double TotalDistance { get; private set; }

        public KMeansResults(T[][] clusters, double[][] means, int[] centroids, double totalDistance)
        {
            Clusters = clusters;
            Means = means;
            Centroids = centroids;
            TotalDistance = totalDistance;
        }
    }
}