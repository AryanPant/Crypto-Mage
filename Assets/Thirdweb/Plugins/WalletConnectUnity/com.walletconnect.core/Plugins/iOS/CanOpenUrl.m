#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

NSString *ConvertCStringToNSString(char* string) {
    return [NSString stringWithUTF8String:string];
}

bool _CanOpenURL (char* url) {
    return [[UIApplication sharedApplication] canOpenURL:[NSURL URLWithString:ConvertCStringToNSString(url)]];
}
