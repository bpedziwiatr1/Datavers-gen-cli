export default class KeywordRequest implements IWebApiRequest {
	private operationName: string;
	private private: string;

	constructor(private private: string) {
		this.operationName = "newkeyword";
		this.private = private;
	}
	getInternalRequest(): IWebApiInternalRequest {
		return {
			private: this.private,
			getMetadata: () => ({
				boundParameter: null,
				parameterTypes: {
					private: {
						typeName: "Edm.String",
						structuralProperty: WebApiRequestStructuralProperty.PrimitiveType
					},
				},
				operationType: WebApiRequestOperationType.Action,
				operationName: this.operationName
			})
		} as IWebApiInternalRequest;
	}
}
