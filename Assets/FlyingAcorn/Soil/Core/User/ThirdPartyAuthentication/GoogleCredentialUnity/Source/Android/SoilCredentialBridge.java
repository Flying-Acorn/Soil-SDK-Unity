package com.flyingacorn.soil.credential;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

/**
 * Same Credential Manager flow as the bundled loginLibary AAR, but also hands the Google ID token
 * to Unity so the server can verify who signed in.
 */
public class SoilCredentialBridge {
    public void getUserDataUnity(String oathClientId, final String objectName, final String methodName,
                                 final String exceptionName) {
        final Activity activity = UnityPlayer.currentActivity;
        GetGoogleIdOption option = new GetGoogleIdOption.Builder()
                .setFilterByAuthorizedAccounts(false)
                .setServerClientId(oathClientId)
                .build();
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
                            UnityPlayer.UnitySendMessage(objectName, methodName, toJson(credential));
                        } catch (Exception e) {
                            sendError(objectName, exceptionName, e.getClass().getSimpleName(), e.getMessage());
                        }
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        sendError(objectName, exceptionName, e.getType(), String.valueOf(e.getMessage()));
                    }
                });
    }

    // Field names match CredentialUserData in CredentialStructs.cs.
    private static String toJson(GoogleIdTokenCredential credential) throws JSONException {
        JSONObject json = new JSONObject();
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

    // Field names match CredentialExceptionData in CredentialStructs.cs.
    private static void sendError(String objectName, String exceptionName, String type, String message) {
        JSONObject json = new JSONObject();
        try {
            json.put("type", type);
            json.put("message", message);
        } catch (JSONException ignored) {
        }
        UnityPlayer.UnitySendMessage(objectName, exceptionName, json.toString());
    }
}
