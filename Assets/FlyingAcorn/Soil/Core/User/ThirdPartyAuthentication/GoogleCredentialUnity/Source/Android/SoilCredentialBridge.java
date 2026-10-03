package com.flyingacorn.soil.credential;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

/**
 * Signs in with Google through Credential Manager and hands the user data, including the Google ID
 * token, to Unity so the server can verify who signed in.
 */
public class SoilCredentialBridge {
    /**
     * Unity calls this; every outcome comes back as exactly one UnitySendMessage that echoes requestId, so
     * the C# side can drop results of requests it has already superseded.
     */
    public void getUserDataUnity(String oathClientId, final String requestId, final String objectName,
                                 final String methodName, final String exceptionName) {
        final Activity activity = UnityPlayer.currentActivity;
        // The full "Sign in with Google" flow, not the account bottom sheet: it is what a sign-in button
        // should open, and it lets the player add a Google account when the device has none.
        GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(oathClientId).build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(option)
                .build();

        CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(),
                activity.getMainExecutor(),
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse result) {
                        try {
                            GoogleIdTokenCredential credential =
                                    GoogleIdTokenCredential.createFrom(result.getCredential().getData());
                            UnityPlayer.UnitySendMessage(objectName, methodName, toJson(credential, requestId));
                        } catch (Exception e) {
                            sendError(objectName, exceptionName, requestId, e.getClass().getSimpleName(),
                                    messageOf(e));
                        }
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        sendError(objectName, exceptionName, requestId, e.getType(), messageOf(e));
                    }
                });
    }

    // Field names match CredentialUserData in CredentialStructs.cs, plus the request id.
    private static String toJson(GoogleIdTokenCredential credential, String requestId) throws JSONException {
        JSONObject json = new JSONObject();
        json.put("requestId", requestId);
        json.put("displayName", credential.getDisplayName());
        json.put("familyName", credential.getFamilyName());
        json.put("givenName", credential.getGivenName());
        json.put("id", credential.getId());
        json.put("phoneNumber", credential.getPhoneNumber());
        json.put("profilePictureUri",
                credential.getProfilePictureUri() == null ? null : credential.getProfilePictureUri().toString());
        json.put("idToken", credential.getIdToken());
        return json.toString();
    }

    // An exception without a message would otherwise reach the player as the text "null".
    private static String messageOf(Exception e) {
        return e.getMessage() == null ? "" : e.getMessage();
    }

    // Field names match CredentialExceptionData in CredentialStructs.cs, plus the request id.
    private static void sendError(String objectName, String exceptionName, String requestId, String type,
                                  String message) {
        JSONObject json = new JSONObject();
        try {
            json.put("requestId", requestId);
            json.put("type", type);
            json.put("message", message);
        } catch (JSONException ignored) {
        }
        UnityPlayer.UnitySendMessage(objectName, exceptionName, json.toString());
    }
}
