#import <Foundation/Foundation.h>
#import <Photos/Photos.h>

@interface iOSPlugin: NSObject


@end


@implementation iOSPlugin

+(void)askPermission{
    //PHPhotoLibrary.requestAuthorization(for: .readWrite);
    PHAuthorizationStatus status = [PHPhotoLibrary authorizationStatus];
    NSString *title = @"status";
    NSString *message;
    if (status == PHAuthorizationStatusAuthorized) {
         // Access has been granted.
        message = @"granted";
    }

    else if (status == PHAuthorizationStatusDenied) {
         // Access has been denied.
        message = @"denied";
    }

    else if (status == PHAuthorizationStatusNotDetermined) {

         // Access has not been determined.
         [PHPhotoLibrary requestAuthorization:^(PHAuthorizationStatus status) {

             if (status == PHAuthorizationStatusAuthorized) {
                 // Access has been granted.
                 
             }

             else {
                 // Access has been denied.
                 
             }
         }];
    }

    else if (status == PHAuthorizationStatusRestricted) {
         // Restricted access - normally won't happen.
        message = @"restricted";
    }
    UIAlertController *alert = [UIAlertController alertControllerWithTitle:title
                                                                       message:message preferredStyle:UIAlertControllerStyleAlert];
        
        UIAlertAction *defaultAction = [UIAlertAction actionWithTitle:@"OK" style:UIAlertActionStyleDefault handler:nil];
        
        [alert addAction:defaultAction];
        [UnityGetGLViewController() presentViewController:alert animated:YES completion:nil];
}

@end


extern "C"
{
    void _AskPermission(){
        [iOSPlugin askPermission];
    }

}
