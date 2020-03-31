using System;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite
{

    /// <summary>
    /// A factory which can be used to create instances of
    /// <see cref="IAudioProvider"/>.
    /// </summary>
    public interface IAudioProviderFactory
    {
        /// <summary>
        /// Checks whether the given <see cref="Uri"/> is supported by this
        /// provider.
        /// </summary>
        /// <param name="location">
        /// The location of the audio file to retrieve.
        /// </param>
        /// <returns>
        /// <code>true</code> if <paramref name="location"/> is supported by
        /// this provider, <code>false</code> otherwise.
        /// </returns>
        bool IsSupported(Uri location);

        /// <summary>
        /// Gets a provider for the given <see cref="Uri"/>.
        /// </summary>
        /// <param name="location">
        /// The location of the audio file to retrieve.
        /// </param>
        /// <returns>
        /// A <see cref="IAudioProvider"/> contianing the state of the
        /// provider.
        /// </returns>
        IAudioProvider GetProvider(Uri location);
    }
}
