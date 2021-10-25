namespace KMeanLibrary
{
    /// <summary>
    /// Delegate that can be passed in to the <see cref="KMeans.Cluster{T}"/> function that allows the caller to provide their own distance calculation function 
    /// for a point to a centroid.
    /// </summary>
    /// <param name="point">the point being calculated</param>
    /// <param name="centroid">the centroid that is being calculated against</param>
    /// <returns>the distance value between the point and the centroid</returns>
    public delegate double KMeansCalculateDistanceDelegate(double[] point, double[] centroid);
}