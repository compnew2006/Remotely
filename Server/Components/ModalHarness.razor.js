/**
 * Shows the modal and notifies .NET whenever the modal is dismissed, no
 * matter how it was closed (the close button, the X, the backdrop, or the
 * escape key).  The content component is disposed when that happens, so
 * anything it was doing in the background stops.
 * @param {HTMLElement} modal
 * @param {import('@microsoft/dotnet/js-interop').DotNetObjectReference} dotNetRef
 */
export function showModal(modal, dotNetRef) {
    const modalApi = new bootstrap.Modal(modal);
    modal.addEventListener('hidden.bs.modal', () => {
        dotNetRef.invokeMethodAsync('OnModalHidden');
    });
    modalApi.show();
}
