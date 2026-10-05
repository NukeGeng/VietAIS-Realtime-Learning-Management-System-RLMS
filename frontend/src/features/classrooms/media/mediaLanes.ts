// SDP media order is part of the peer protocol: audio, presentation, camera.
export function createMediaLanes(peer: RTCPeerConnection) {
  return {
    audio: peer.addTransceiver('audio', { direction: 'sendrecv' }),
    screen: peer.addTransceiver('video', { direction: 'sendrecv' }),
    camera: peer.addTransceiver('video', { direction: 'sendrecv' })
  }
}

export function receiveMediaLanes(peer: RTCPeerConnection) {
  const [audio, screen, camera] = peer.getTransceivers()
  if (!audio || !screen || !camera || audio.receiver.track.kind !== 'audio' ||
    screen.receiver.track.kind !== 'video' || camera.receiver.track.kind !== 'video') {
    throw new Error('Missing media lanes')
  }
  for (const lane of [audio, screen, camera]) lane.direction = 'sendrecv'
  return { audio, screen, camera }
}
