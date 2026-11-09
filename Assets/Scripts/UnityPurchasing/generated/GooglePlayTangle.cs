// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("NYcEJzUIAwwvg02D8ggEBAQABQbZI+t9d+rRVKZySwNOzWTwICsWVYErBIi0hTnMBmzEL4nHgIduuP9t0XxeJLeMQICcZvNFTgxFahMTexWMj7/tXxcCEFi4THjj8QHZsDEICKd4IuqziZxBdbI8Xo8BiqxR+pTeXgYcldgj0QsW7WgvmMOHEWfUEh0Nc2/YdEoJG8vdcf+TWDMoE43024cECgU1hwQPB4cEBAWljtyjGFs5Iu9Z6l2FQqaxvzt9r/VJpN2TR73JESLpoWznoCZ10Zd8J9Wbo9Ss0VQ9rluZERxHkaN334KdUhELcsD8I0jRu6c+pZxvtIaHanpIiXc+x5YvcDXFnZU4IMVTfQFqaXAgiJ/bh1xVuH9VM5n2SAcGBAUE");
        private static int[] order = new int[] { 0,4,9,6,13,7,9,12,13,12,10,11,12,13,14 };
        private static int key = 5;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
